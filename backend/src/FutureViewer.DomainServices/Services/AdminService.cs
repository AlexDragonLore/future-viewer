using System.Security.Cryptography;
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
    private readonly IFeedbackRepository _feedbacks;
    private readonly IReadingRepository _readings;
    private readonly IUserRepository _users;
    private readonly IAchievementRepository _achievementsRepo;
    private readonly AchievementService _achievements;
    private readonly TelegramLinkService _telegram;
    private readonly ILogger<AdminService> _logger;

    public AdminService(
        IFeedbackRepository feedbacks,
        IReadingRepository readings,
        IUserRepository users,
        IAchievementRepository achievementsRepo,
        AchievementService achievements,
        TelegramLinkService telegram,
        ILogger<AdminService> logger)
    {
        _feedbacks = feedbacks;
        _readings = readings;
        _users = users;
        _achievementsRepo = achievementsRepo;
        _achievements = achievements;
        _telegram = telegram;
        _logger = logger;
    }

    public async Task<AdminFeedbackListResult> SearchFeedbacksAsync(
        Guid? userId,
        FeedbackStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var safePage = Math.Max(1, page);
        var safeSize = Math.Clamp(pageSize, 1, 100);
        var skip = (safePage - 1) * safeSize;

        var items = await _feedbacks.SearchAsync(userId, status, skip, safeSize, ct);
        var total = await _feedbacks.CountAsync(userId, status, ct);

        return new AdminFeedbackListResult
        {
            Items = items.Select(MapAdmin).ToList(),
            Total = total
        };
    }

    public async Task<AdminFeedbackDto> CreateScheduledAsync(
        Guid actorId,
        string actorEmail,
        Guid readingId,
        DateTime? scheduledAt,
        bool bypassDelay,
        bool replace,
        CancellationToken ct = default)
    {
        var reading = await _readings.GetByIdAsync(readingId, ct)
            ?? throw new NotFoundException("Reading not found");
        if (reading.UserId is null)
            throw new DomainException("Cannot create feedback for anonymous reading");

        var existing = await _feedbacks.GetByReadingIdAsync(readingId, ct);
        if (existing is not null)
        {
            if (!replace)
                throw new ConflictException("Feedback already exists for this reading. Pass replace=true to overwrite.");
            await _feedbacks.DeleteAsync(existing.Id, ct);
        }

        var now = DateTime.UtcNow;
        var effectiveScheduledAt = bypassDelay
            ? now.AddMinutes(-1)
            : (scheduledAt ?? now + FeedbackService.ScheduleDelay);

        var feedback = new ReadingFeedback
        {
            ReadingId = reading.Id,
            UserId = reading.UserId.Value,
            Token = GenerateToken(),
            ScheduledAt = effectiveScheduledAt,
            CreatedAt = now,
            Status = FeedbackStatus.Pending
        };

        var saved = await _feedbacks.AddAsync(feedback, ct);

        _logger.LogInformation(
            "Admin {ActorEmail} ({ActorId}) created feedback {FeedbackId} for reading {ReadingId} (bypassDelay={Bypass}, replace={Replace})",
            actorEmail, actorId, saved.Id, readingId, bypassDelay, replace);

        return MapAdmin(saved, reading, await _users.GetByIdAsync(reading.UserId.Value, ct));
    }

    public async Task<AdminFeedbackDto> CreateSyntheticAsync(
        Guid actorId,
        string actorEmail,
        Guid readingId,
        int aiScore,
        string? aiScoreReason,
        bool isSincere,
        string? selfReport,
        CancellationToken ct = default)
    {
        if (aiScore is < 1 or > 10)
            throw new DomainException("aiScore must be between 1 and 10");

        var reading = await _readings.GetByIdAsync(readingId, ct)
            ?? throw new NotFoundException("Reading not found");
        if (reading.UserId is null)
            throw new DomainException("Cannot create synthetic feedback for anonymous reading");

        var existing = await _feedbacks.GetByReadingIdAsync(readingId, ct);
        if (existing is not null)
            throw new ConflictException("Feedback already exists for this reading. Edit or delete the existing feedback instead.");

        var now = DateTime.UtcNow;
        var feedback = new ReadingFeedback
        {
            ReadingId = reading.Id,
            UserId = reading.UserId.Value,
            Token = GenerateToken(),
            ScheduledAt = now,
            CreatedAt = now,
            NotifiedAt = now,
            AnsweredAt = now,
            Status = FeedbackStatus.Scored,
            AiScore = aiScore,
            AiScoreReason = aiScoreReason,
            IsSincere = isSincere,
            SelfReport = selfReport
        };

        var saved = await _feedbacks.AddAsync(feedback, ct);

        _logger.LogInformation(
            "Admin {ActorEmail} ({ActorId}) created synthetic feedback {FeedbackId} for reading {ReadingId} score={Score}",
            actorEmail, actorId, saved.Id, readingId, aiScore);

        await _achievements.CheckAndGrantAsync(saved.UserId, ct);

        return MapAdmin(saved, reading, await _users.GetByIdAsync(reading.UserId.Value, ct));
    }

    public async Task<AdminFeedbackDto> UpdateAsync(
        Guid actorId,
        string actorEmail,
        Guid feedbackId,
        AdminFeedbackUpdate update,
        CancellationToken ct = default)
    {
        var feedback = await _feedbacks.GetByIdAsync(feedbackId, ct)
            ?? throw new NotFoundException("Feedback not found");

        if (update.AiScore is { } score)
        {
            if (score is < 1 or > 10)
                throw new DomainException("aiScore must be between 1 and 10");
            feedback.AiScore = score;
        }
        if (update.AiScoreReason is not null) feedback.AiScoreReason = update.AiScoreReason;
        if (update.IsSincere.HasValue) feedback.IsSincere = update.IsSincere;
        if (update.Status.HasValue) feedback.Status = update.Status.Value;
        if (update.SelfReport is not null) feedback.SelfReport = update.SelfReport;
        if (update.ScheduledAt.HasValue) feedback.ScheduledAt = update.ScheduledAt.Value;
        if (update.NotifiedAt.HasValue) feedback.NotifiedAt = update.NotifiedAt;
        if (update.AnsweredAt.HasValue) feedback.AnsweredAt = update.AnsweredAt;

        await _feedbacks.UpdateAsync(feedback, ct);

        _logger.LogInformation(
            "Admin {ActorEmail} ({ActorId}) updated feedback {FeedbackId}: score={Score} status={Status}",
            actorEmail, actorId, feedbackId, feedback.AiScore, feedback.Status);

        await _achievements.CheckAndGrantAsync(feedback.UserId, ct);

        var reading = feedback.Reading ?? await _readings.GetByIdAsync(feedback.ReadingId, ct);
        return MapAdmin(feedback, reading, await _users.GetByIdAsync(feedback.UserId, ct));
    }

    public async Task DeleteFeedbackAsync(
        Guid actorId,
        string actorEmail,
        Guid feedbackId,
        CancellationToken ct = default)
    {
        var existed = await _feedbacks.DeleteAsync(feedbackId, ct);
        if (!existed)
            throw new NotFoundException("Feedback not found");

        _logger.LogInformation(
            "Admin {ActorEmail} ({ActorId}) deleted feedback {FeedbackId}",
            actorEmail, actorId, feedbackId);
    }

    public async Task<AdminUserListResult> SearchUsersAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var safePage = Math.Max(1, page);
        var safeSize = Math.Clamp(pageSize, 1, 100);
        var skip = (safePage - 1) * safeSize;

        var users = await _users.SearchAsync(search, skip, safeSize, ct);
        var total = await _users.CountAsync(search, ct);

        var items = new List<AdminUserListItem>(users.Count);
        foreach (var user in users)
        {
            items.Add(await BuildListItemAsync(user, ct));
        }

        return new AdminUserListResult
        {
            Items = items,
            Total = total
        };
    }

    public async Task<AdminUserDetailDto> GetUserDetailAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("User not found");

        var totalReadings = await _readings.CountByUserAsync(userId, ct);
        var scoredFeedbacks = await _feedbacks.GetScoredByUserAsync(userId, ct);
        var totalScore = scoredFeedbacks.Sum(f => f.AiScore ?? 0);
        var allFeedbacksCount = await _feedbacks.CountAsync(userId, null, ct);

        var recentReadings = await _readings.GetByUserAsync(userId, 20, ct);
        var recentFeedbacks = await _feedbacks.GetByUserAsync(userId, 20, ct);
        var userAchievements = await _achievementsRepo.GetByUserAsync(userId, ct);
        var allAchievements = await _achievementsRepo.GetAllAsync(ct);
        var byId = allAchievements.ToDictionary(a => a.Id, a => a);

        return new AdminUserDetailDto
        {
            Id = user.Id,
            Email = user.Email,
            CreatedAt = user.CreatedAt,
            IsAdmin = user.IsAdmin,
            SubscriptionStatus = user.SubscriptionStatus,
            SubscriptionExpiresAt = user.SubscriptionExpiresAt,
            YukassaSubscriptionId = user.YukassaSubscriptionId,
            TelegramChatId = user.TelegramChatId,
            HasTelegramLinkToken = !string.IsNullOrEmpty(user.TelegramLinkToken),
            TotalReadings = totalReadings,
            TotalFeedbacks = allFeedbacksCount,
            TotalScore = totalScore,
            RecentReadings = recentReadings.Select(r => new AdminReadingSummary
            {
                Id = r.Id,
                Question = r.Question,
                SpreadType = r.SpreadType,
                DeckType = r.DeckType,
                CreatedAt = r.CreatedAt
            }).ToList(),
            RecentFeedbacks = recentFeedbacks.Select(f => MapAdmin(f, f.Reading, user)).ToList(),
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
            _logger.LogInformation(
                "Admin {ActorEmail} ({ActorId}) set IsAdmin={IsAdmin} on user {UserId}",
                actorEmail, actorId, isAdmin, userId);
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

        var deleted = await _users.DeleteAsync(userId, ct);
        if (!deleted)
            throw new NotFoundException("User not found");

        _logger.LogInformation(
            "Admin {ActorEmail} ({ActorId}) deleted user {UserId}",
            actorEmail, actorId, userId);
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

        _logger.LogInformation(
            "Admin {ActorEmail} ({ActorId}) set subscription on {UserId}: status={Status} expiresAt={ExpiresAt}",
            actorEmail, actorId, userId, status, user.SubscriptionExpiresAt);

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

        _logger.LogInformation(
            "Admin {ActorEmail} ({ActorId}) granted achievement {Code} to user {UserId}",
            actorEmail, actorId, code, userId);

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

        var achievement = await _achievementsRepo.GetByCodeAsync(code, ct)
            ?? throw new NotFoundException($"Achievement '{code}' not found");

        var removed = await _achievementsRepo.RevokeAsync(userId, achievement.Id, ct);
        if (!removed)
            throw new NotFoundException($"User does not have achievement '{code}'");

        _logger.LogInformation(
            "Admin {ActorEmail} ({ActorId}) revoked achievement {Code} from user {UserId}",
            actorEmail, actorId, code, userId);
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

        _logger.LogInformation(
            "Admin {ActorEmail} ({ActorId}) rechecked achievements for user {UserId}: granted={Count}",
            actorEmail, actorId, userId, granted.Count);

        return granted;
    }

    public async Task UnlinkTelegramAsync(
        Guid actorId,
        string actorEmail,
        Guid userId,
        CancellationToken ct = default)
    {
        _ = await _users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("User not found");

        await _telegram.UnlinkAsync(userId, ct);

        _logger.LogInformation(
            "Admin {ActorEmail} ({ActorId}) unlinked Telegram for user {UserId}",
            actorEmail, actorId, userId);
    }

    public async Task<AdminStatsDto> GetStatsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var todayUtc = now.Date;
        var weekAgoUtc = now.AddDays(-7);
        var monthAgoUtc = now.AddDays(-30);

        var totalUsers = await _users.CountAsync(null, ct);
        var adminCount = await _users.CountAdminsAsync(ct);
        var activeSubs = await _users.CountActiveSubscriptionsAsync(now, ct);
        var readingsToday = await _readings.CountSinceAsync(todayUtc, ct);
        var readingsThisWeek = await _readings.CountSinceAsync(weekAgoUtc, ct);
        var pendingToNotify = await _feedbacks.CountPendingToNotifyAsync(now, ct);
        var scoredThisMonth = await _feedbacks.CountScoredSinceAsync(monthAgoUtc, ct);

        return new AdminStatsDto
        {
            TotalUsers = totalUsers,
            AdminCount = adminCount,
            ActiveSubscriptions = activeSubs,
            ReadingsToday = readingsToday,
            ReadingsThisWeek = readingsThisWeek,
            PendingFeedbacksToNotify = pendingToNotify,
            ScoredFeedbacksThisMonth = scoredThisMonth
        };
    }

    public async Task<AdminTelegramLinkResult> SetTelegramChatIdAsync(
        Guid actorId,
        string actorEmail,
        Guid userId,
        long chatId,
        CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("User not found");

        var existing = await _users.GetByTelegramChatIdAsync(chatId, ct);
        if (existing is not null && existing.Id != userId)
            throw new ConflictException($"Telegram chatId {chatId} is already linked to another user");

        user.TelegramChatId = chatId;
        user.TelegramLinkToken = null;

        try
        {
            await _users.UpdateAsync(user, ct);
        }
        catch (Exception ex) when (IsUniqueViolation(ex))
        {
            throw new ConflictException($"Telegram chatId {chatId} is already linked to another user");
        }

        _logger.LogInformation(
            "Admin {ActorEmail} ({ActorId}) set Telegram chatId={ChatId} for user {UserId}",
            actorEmail, actorId, chatId, userId);

        return new AdminTelegramLinkResult { ChatId = chatId };
    }

    private static bool IsUniqueViolation(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
        {
            var name = e.GetType().Name;
            if (name.Contains("UniqueConstraint", StringComparison.OrdinalIgnoreCase))
                return true;
            if (e.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
                e.Message.Contains("unique constraint", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private async Task<AdminUserListItem> BuildListItemAsync(User user, CancellationToken ct)
    {
        var totalReadings = await _readings.CountByUserAsync(user.Id, ct);
        var totalFeedbacks = await _feedbacks.CountAsync(user.Id, null, ct);
        var scored = await _feedbacks.GetScoredByUserAsync(user.Id, ct);
        var totalScore = scored.Sum(f => f.AiScore ?? 0);

        return new AdminUserListItem
        {
            Id = user.Id,
            Email = user.Email,
            CreatedAt = user.CreatedAt,
            IsAdmin = user.IsAdmin,
            SubscriptionStatus = user.SubscriptionStatus,
            SubscriptionExpiresAt = user.SubscriptionExpiresAt,
            TelegramChatId = user.TelegramChatId,
            TotalReadings = totalReadings,
            TotalFeedbacks = totalFeedbacks,
            TotalScore = totalScore
        };
    }

    private static AdminFeedbackDto MapAdmin(ReadingFeedback feedback) =>
        MapAdmin(feedback, feedback.Reading, feedback.User);

    private static AdminFeedbackDto MapAdmin(ReadingFeedback feedback, Reading? reading, User? user) =>
        new()
        {
            Id = feedback.Id,
            ReadingId = feedback.ReadingId,
            UserId = feedback.UserId,
            UserEmail = user?.Email,
            Question = reading?.Question,
            Token = feedback.Token,
            SelfReport = feedback.SelfReport,
            AiScore = feedback.AiScore,
            AiScoreReason = feedback.AiScoreReason,
            IsSincere = feedback.IsSincere,
            ScheduledAt = feedback.ScheduledAt,
            NotifiedAt = feedback.NotifiedAt,
            AnsweredAt = feedback.AnsweredAt,
            Status = feedback.Status,
            CreatedAt = feedback.CreatedAt
        };

    private static string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[24];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", string.Empty);
    }
}

public sealed class AdminFeedbackListResult
{
    public required IReadOnlyList<AdminFeedbackDto> Items { get; init; }
    public required int Total { get; init; }
}

public sealed class AdminFeedbackUpdate
{
    public int? AiScore { get; init; }
    public string? AiScoreReason { get; init; }
    public bool? IsSincere { get; init; }
    public FeedbackStatus? Status { get; init; }
    public string? SelfReport { get; init; }
    public DateTime? ScheduledAt { get; init; }
    public DateTime? NotifiedAt { get; init; }
    public DateTime? AnsweredAt { get; init; }
}

public sealed class AdminUserListResult
{
    public required IReadOnlyList<AdminUserListItem> Items { get; init; }
    public required int Total { get; init; }
}

public sealed class AdminTelegramLinkResult
{
    public bool Linked { get; init; } = true;
    public required long ChatId { get; init; }
}
