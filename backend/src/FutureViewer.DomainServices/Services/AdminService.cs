using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.DTOs.Admin;
using FutureViewer.DomainServices.Exceptions;
using FutureViewer.DomainServices.Interfaces;
using Microsoft.Extensions.Logging;

namespace FutureViewer.DomainServices.Services;

public sealed class AdminService
{
    private readonly IReadingRepository _readings;
    private readonly IUserRepository _users;
    private readonly IAchievementRepository _achievementsRepo;
    private readonly AchievementService _achievements;
    private readonly ILogger<AdminService> _logger;
    private readonly IPrivacyRepository? _privacy;
    private readonly IPaymentOrderRepository? _orders;

    public AdminService(
        IReadingRepository readings,
        IUserRepository users,
        IAchievementRepository achievementsRepo,
        AchievementService achievements,
        ILogger<AdminService> logger,
        IPrivacyRepository? privacy = null,
        IPaymentOrderRepository? orders = null)
    {
        _readings = readings;
        _users = users;
        _achievementsRepo = achievementsRepo;
        _achievements = achievements;
        _logger = logger;
        _privacy = privacy;
        _orders = orders;
    }

    public async Task<AdminReadingListResult> SearchReadingsAsync(
        Guid? userId,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default,
        bool includeInterpretation = true,
        AdminReadingAudience audience = AdminReadingAudience.All,
        AdminReadingReviewFilter? review = null)
    {
        var safeSize = Math.Clamp(pageSize, 1, 100);
        var safePage = Math.Clamp(page, 1, int.MaxValue / safeSize);
        var skip = (safePage - 1) * safeSize;
        // Use one instant for access/retention filtering, row metadata and the total.
        var now = DateTime.UtcNow;
        var readings = await _readings.SearchAdminListAsync(userId, search, skip, safeSize, includeInterpretation, ct, audience, now, review);
        var total = await _readings.CountForAdminAsync(userId, search, ct, audience, now, review);
        await AttachPurchasesAsync(readings.Where(r => r.UserId != null && r.Subscription != null)
            .Select(r => (r.UserId!.Value, r.Subscription!)), ct);
        return new AdminReadingListResult
        {
            Items = readings,
            Total = total
        };
    }

    public async Task MarkReadingsViewedAsync(Guid actorId, string actorEmail, Guid[] readingIds, CancellationToken ct = default)
    {
        await _readings.MarkAdminReadingsViewedAsync(actorId, readingIds.Distinct().ToArray(), ct);
        _logger.LogInformation("Admin {ActorUserId} ({ActorEmail}) action {Action} target {TargetId}",
            actorId, actorEmail, "readings.viewed", string.Join(",", readingIds));
    }

    public async Task<AdminReadingInterpretationDto> GetReadingInterpretationAsync(Guid id, CancellationToken ct = default) =>
        await _readings.GetAdminInterpretationAsync(id, ct) ?? throw new NotFoundException("Reading not found");

    public async Task<AdminUserListResult> SearchUsersAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var safeSize = Math.Clamp(pageSize, 1, 100);
        var safePage = Math.Clamp(page, 1, int.MaxValue / safeSize);
        var skip = (safePage - 1) * safeSize;

        var users = await _users.SearchAdminListAsync(search, skip, safeSize, ct);
        var total = await _users.CountAsync(search, ct);
        await AttachPurchasesAsync(users.Where(u => u.Subscription != null).Select(u => (u.Id, u.Subscription!)), ct);

        return new AdminUserListResult
        {
            Items = users,
            Total = total
        };
    }

    public async Task<AdminUserDetailDto> GetUserDetailAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("User not found");

        var totalReadings = await _readings.CountByUserAsync(userId, ct);

        var recentReadings = await _readings.SearchForAdminAsync(userId, null, 0, 20, ct);
        var userAchievements = await _achievementsRepo.GetByUserAsync(userId, ct);
        var allAchievements = await _achievementsRepo.GetAllAsync(ct);
        var byId = allAchievements.ToDictionary(a => a.Id, a => a);
        var subscription = BuildSubscription(user);
        await AttachPurchasesAsync([(user.Id, subscription)], ct);

        return new AdminUserDetailDto
        {
            Id = user.Id,
            Email = user.Email,
            CreatedAt = user.CreatedAt,
            IsAdmin = user.IsAdmin,
            SubscriptionStatus = user.SubscriptionStatus,
            SubscriptionExpiresAt = user.SubscriptionExpiresAt,
            Subscription = subscription,
            YukassaSubscriptionId = user.YukassaSubscriptionId,
            TotalReadings = totalReadings,
            RecentReadings = recentReadings.Select(r => new AdminReadingSummary
            {
                Id = r.Id,
                Question = r.Question,
                HasInterpretation = !string.IsNullOrWhiteSpace(r.AiInterpretation),
                Interpretation = r.AiInterpretation,
                SpreadType = r.SpreadType,
                DeckType = r.DeckType,
                CreatedAt = r.CreatedAt,
                DeletedFromHistoryAt = r.DeletedFromHistoryAt
            }).ToList(),
            Achievements = userAchievements
                .Where(ua => byId.ContainsKey(ua.AchievementId))
                .OrderByDescending(ua => ua.UnlockedAt)
                .Select(ua => new AdminAchievementDto
                {
                    Id = ua.AchievementId,
                    Code = byId[ua.AchievementId].Code,
                    Name = byId[ua.AchievementId].NameRu,
                    UnlockedAt = ua.UnlockedAt
                }).ToList()
        };
    }

    public async Task<AdminUserListItem> SetAdminAsync(
        Guid actorId,
        string actorEmail,
        Guid userId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("User not found");

        if (actorId == userId && !isAdmin)
            throw new ConflictException("Admin cannot revoke their own admin role");

        if (user.IsAdmin != isAdmin)
        {
            user.IsAdmin = isAdmin;
            await _users.UpdateAsync(user, ct);
            await WriteAuditAsync(
                actorId,
                "admin.user_role_changed",
                "user",
                user.PrivacySubjectId,
                ct,
                isAdmin ? "admin_enabled" : "admin_disabled");
        }

        return await BuildListItemAsync(user, ct);
    }

    public async Task DeleteUserAsync(
        Guid actorId,
        string actorEmail,
        Guid userId,
        CancellationToken ct = default)
    {
        if (actorId == userId)
            throw new ConflictException("Admin cannot delete themselves");

        var target = await _users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("User not found");
        var targetReference = target.PrivacySubjectId;

        var deleted = await _users.DeleteAsync(userId, ct);
        if (!deleted)
            throw new NotFoundException("User not found");

        await WriteAuditAsync(
            actorId,
            "admin.user_deleted",
            "user",
            targetReference,
            ct);
    }

    public async Task<AdminUserDetailDto> SetSubscriptionAsync(
        Guid actorId,
        string actorEmail,
        Guid userId,
        SubscriptionStatus status,
        DateTime? expiresAt,
        CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("User not found");

        user.SubscriptionStatus = status;
        if (status == SubscriptionStatus.Active)
        {
            user.SubscriptionExpiresAt = expiresAt ?? DateTime.UtcNow.AddDays(SubscriptionService.SubscriptionDurationDays);
        }
        else
        {
            user.SubscriptionExpiresAt = expiresAt;
        }

        await _users.UpdateAsync(user, ct);

        await WriteAuditAsync(
            actorId,
            "admin.subscription_changed",
            "user",
            user.PrivacySubjectId,
            ct,
            status.ToString().ToLowerInvariant());

        return await GetUserDetailAsync(userId, ct);
    }

    public async Task<AchievementDto> GrantAchievementAsync(
        Guid actorId,
        string actorEmail,
        Guid userId,
        string code,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new DomainException("Achievement code is required");

        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("User not found");

        var achievement = await _achievementsRepo.GetByCodeAsync(code, ct)
            ?? throw new NotFoundException($"Achievement '{code}' not found");

        var now = DateTime.UtcNow;
        var granted = await _achievementsRepo.GrantAsync(new UserAchievement
        {
            UserId = user.Id,
            AchievementId = achievement.Id,
            UnlockedAt = now
        }, ct);

        if (granted is null)
            throw new ConflictException($"User already has achievement '{code}'");

        await WriteAuditAsync(
            actorId,
            "admin.achievement_granted",
            "user",
            user.PrivacySubjectId,
            ct);

        return new AchievementDto
        {
            Id = achievement.Id,
            Code = achievement.Code,
            Name = achievement.NameRu,
            Description = achievement.DescriptionRu,
            IconPath = achievement.IconPath,
            SortOrder = achievement.SortOrder,
            UnlockedAt = granted.UnlockedAt
        };
    }

    public async Task RevokeAchievementAsync(
        Guid actorId,
        string actorEmail,
        Guid userId,
        string code,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new DomainException("Achievement code is required");

        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("User not found");
        var achievement = await _achievementsRepo.GetByCodeAsync(code, ct)
            ?? throw new NotFoundException($"Achievement '{code}' not found");

        var removed = await _achievementsRepo.RevokeAsync(userId, achievement.Id, ct);
        if (!removed)
            throw new NotFoundException($"User does not have achievement '{code}'");

        await WriteAuditAsync(
            actorId,
            "admin.achievement_revoked",
            "user",
            user.PrivacySubjectId,
            ct);
    }

    public async Task<IReadOnlyList<AchievementDto>> RecheckAchievementsAsync(
        Guid actorId,
        string actorEmail,
        Guid userId,
        CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("User not found");

        var granted = await _achievements.CheckAndGrantAsync(user.Id, ct);

        await WriteAuditAsync(
            actorId,
            "admin.achievements_rechecked",
            "user",
            user.PrivacySubjectId,
            ct,
            granted.Count > 0 ? "changes_applied" : "no_changes");

        return granted;
    }

    public async Task<AdminStatsDto> GetStatsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var todayUtc = now.Date;
        var weekAgoUtc = now.AddDays(-7);

        var totalUsers = await _users.CountAsync(null, ct);
        var adminCount = await _users.CountAdminsAsync(ct);
        var activeSubs = await _users.CountActiveSubscriptionsAsync(now, ct);
        var readingsToday = await _readings.CountSinceAsync(todayUtc, ct);
        var readingsThisWeek = await _readings.CountSinceAsync(weekAgoUtc, ct);

        return new AdminStatsDto
        {
            TotalUsers = totalUsers,
            AdminCount = adminCount,
            ActiveSubscriptions = activeSubs,
            ReadingsToday = readingsToday,
            ReadingsThisWeek = readingsThisWeek
        };
    }

    private async Task WriteAuditAsync(
        Guid actorId,
        string eventType,
        string targetType,
        Guid? targetReference,
        CancellationToken ct,
        string? reasonCode = null)
    {
        if (_privacy is null)
        {
            // Compatibility path for isolated unit tests that construct the legacy
            // service directly. Production DI always provides IPrivacyRepository.
            _logger.LogWarning(
                "Persistent administrative audit is unavailable; eventType={EventType}",
                eventType);
            return;
        }

        var actor = await _users.GetByIdAsync(actorId, ct);
        var auditEvent = new AuditEvent
        {
            OccurredAt = DateTime.UtcNow,
            EventType = eventType,
            CorrelationId = Guid.NewGuid().ToString("N"),
            ActorSubjectReference = actor?.PrivacySubjectId,
            TargetType = targetType,
            TargetReference = targetReference,
            Outcome = "success",
            ReasonCode = reasonCode
        };
        await _privacy.AddAuditEventAsync(auditEvent, ct);
        _logger.LogInformation(
            "Administrative mutation audited; eventType={EventType}; auditEventId={AuditEventId}",
            eventType,
            auditEvent.Id);
    }

    private async Task<AdminUserListItem> BuildListItemAsync(User user, CancellationToken ct)
    {
        var totalReadings = await _readings.CountByUserAsync(user.Id, ct);
        var subscription = BuildSubscription(user);
        await AttachPurchasesAsync([(user.Id, subscription)], ct);

        return new AdminUserListItem
        {
            Id = user.Id,
            Email = user.Email,
            CreatedAt = user.CreatedAt,
            IsAdmin = user.IsAdmin,
            SubscriptionStatus = user.SubscriptionStatus,
            SubscriptionExpiresAt = user.SubscriptionExpiresAt,
            Subscription = subscription,
            TotalReadings = totalReadings
        };
    }

    private static AdminSubscriptionDto BuildSubscription(User user) => new()
    {
        Status = user.SubscriptionStatus,
        IsActive = user.SubscriptionStatus == SubscriptionStatus.Active
            && user.SubscriptionExpiresAt is { } expiresAt && expiresAt > DateTime.UtcNow,
        ExpiresAt = user.SubscriptionExpiresAt
    };

    private async Task AttachPurchasesAsync(
        IEnumerable<(Guid UserId, AdminSubscriptionDto Subscription)> subscriptions, CancellationToken ct)
    {
        var rows = subscriptions.ToArray();
        if (_orders is null || rows.Length == 0) return;
        var purchases = await _orders.GetLatestPaidSubscriptionsAsync(rows.Select(r => r.UserId).Distinct().ToArray(), ct);
        foreach (var row in rows)
            row.Subscription.LastPurchase = purchases.GetValueOrDefault(row.UserId);
    }
}

public sealed class AdminUserListResult
{
    public required IReadOnlyList<AdminUserListItem> Items { get; init; }
    public required int Total { get; init; }
}
