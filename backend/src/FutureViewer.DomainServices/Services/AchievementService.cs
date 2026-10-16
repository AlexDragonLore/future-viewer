using FutureViewer.Domain.Entities;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Exceptions;
using FutureViewer.DomainServices.Interfaces;

namespace FutureViewer.DomainServices.Services;

public sealed class AchievementService
{
    public static class Codes
    {
        public const string FirstReading = "first_reading";
        public const string FirstFeedback = "first_feedback";
        public const string Streak3 = "streak_3";
        public const string Streak7 = "streak_7";
        public const string Streak30 = "streak_30";
        public const string Total10 = "total_10";
        public const string Total50 = "total_50";
        public const string Total100 = "total_100";
        public const string ScoreMaster = "score_master";
        public const string Perfect10 = "perfect_10";
        public const string HighFive = "high_five";
    }

    private readonly IAchievementRepository _achievements;
    private readonly IReadingRepository _readings;
    private readonly IFeedbackRepository _feedbacks;
    private readonly IUserRepository _users;

    public AchievementService(
        IAchievementRepository achievements,
        IReadingRepository readings,
        IFeedbackRepository feedbacks,
        IUserRepository users)
    {
        _achievements = achievements;
        _readings = readings;
        _feedbacks = feedbacks;
        _users = users;
    }

    public async Task<IReadOnlyList<AchievementDto>> CheckAndGrantAsync(Guid userId, CancellationToken ct = default) =>
        (await CheckAsync(userId, includeStatus: false, ct)).NewlyGranted;

    public async Task<IReadOnlyList<AchievementDto>> CheckAndGetAllAsync(Guid userId, CancellationToken ct = default)
    {
        var result = await CheckAsync(userId, includeStatus: true, ct);
        var unlocked = result.Owned.ToDictionary(x => x.AchievementId, x => x.UnlockedAt);
        return result.Catalog.OrderBy(a => a.SortOrder)
            .Select(a => MapAchievement(a, unlocked.TryGetValue(a.Id, out var at) ? at : null)).ToList();
    }

    private async Task<AchievementCheck> CheckAsync(Guid userId, bool includeStatus, CancellationToken ct)
    {
        _ = await _users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("User not found");
        var readingCount = await _readings.CountByUserAsync(userId, ct);
        var scores = await _feedbacks.GetScoreStatsAsync(userId, ct);
        var maxStreak = 0;
        if (readingCount > 0)
        {
            var dates = await _readings.GetDistinctReadingDatesAsync(userId, DateTime.UtcNow.Date.AddDays(-40), ct);
            maxStreak = ComputeCurrentStreak(dates);
        }
        var codes = new List<string>();
        if (readingCount >= 1) codes.Add(Codes.FirstReading);
        if (readingCount >= 10) codes.Add(Codes.Total10);
        if (readingCount >= 50) codes.Add(Codes.Total50);
        if (readingCount >= 100) codes.Add(Codes.Total100);
        if (scores.ScoredCount >= 1) codes.Add(Codes.FirstFeedback);
        if (maxStreak >= 3) codes.Add(Codes.Streak3);
        if (maxStreak >= 7) codes.Add(Codes.Streak7);
        if (maxStreak >= 30) codes.Add(Codes.Streak30);
        if (scores.Perfect10Count >= 1) codes.Add(Codes.Perfect10);
        if (scores.Perfect10Count >= 5) codes.Add(Codes.HighFive);
        if (scores.ScoredCount >= 10 && scores.AverageScore >= 8d) codes.Add(Codes.ScoreMaster);
        if (codes.Count == 0 && !includeStatus) return new([], [], []);

        var all = await _achievements.GetAllAsync(ct);
        var existing = await _achievements.GetByUserAsync(userId, ct);
        var existingIds = existing.Select(x => x.AchievementId).ToHashSet();
        var byCode = all.ToDictionary(a => a.Code);
        var now = DateTime.UtcNow;
        var pending = codes.Where(byCode.ContainsKey).Select(code => byCode[code])
            .Where(a => !existingIds.Contains(a.Id))
            .Select(a => new UserAchievement { UserId = userId, AchievementId = a.Id, UnlockedAt = now }).ToArray();
        if (pending.Length == 0) return new(all, existing, []);
        var granted = await _achievements.GrantManyAsync(pending, ct);
        // If another check won a unique-key race, use its actual persisted timestamp.
        var owned = includeStatus && granted.Count < pending.Length
            ? await _achievements.GetByUserAsync(userId, ct)
            : existing.Concat(granted).ToArray();
        var byId = all.ToDictionary(a => a.Id);
        var newlyGranted = granted.Select(x => MapAchievement(byId[x.AchievementId], x.UnlockedAt)).ToArray();
        return new(all, owned, newlyGranted);
    }

    private sealed record AchievementCheck(
        IReadOnlyList<Achievement> Catalog,
        IReadOnlyList<UserAchievement> Owned,
        IReadOnlyList<AchievementDto> NewlyGranted);

    public async Task<IReadOnlyList<AchievementDto>> GetAllWithUserStatusAsync(Guid userId, CancellationToken ct = default)
    {
        var all = await _achievements.GetAllAsync(ct);
        var userAchievements = await _achievements.GetByUserAsync(userId, ct);
        var unlockedById = userAchievements.ToDictionary(ua => ua.AchievementId, ua => ua.UnlockedAt);

        return all
            .OrderBy(a => a.SortOrder)
            .Select(a => MapAchievement(a, unlockedById.TryGetValue(a.Id, out var dt) ? dt : null))
            .ToList();
    }

    public async Task<IReadOnlyList<AchievementDto>> GetUserAchievementsAsync(Guid userId, CancellationToken ct = default)
    {
        var userAchievements = await _achievements.GetByUserAsync(userId, ct);
        var all = await _achievements.GetAllAsync(ct);
        var byId = all.ToDictionary(a => a.Id, a => a);
        return userAchievements
            .Where(ua => byId.ContainsKey(ua.AchievementId))
            .OrderByDescending(ua => ua.UnlockedAt)
            .Select(ua => MapAchievement(byId[ua.AchievementId], ua.UnlockedAt))
            .ToList();
    }

    private static int ComputeCurrentStreak(IReadOnlyList<DateTime> dates)
    {
        if (dates.Count == 0) return 0;

        var today = DateTime.UtcNow.Date;
        var sorted = dates.Select(d => d.Date).Distinct().OrderByDescending(d => d).ToList();
        if (sorted[0] != today && sorted[0] != today.AddDays(-1))
            return 0;

        var streak = 1;
        for (var i = 1; i < sorted.Count; i++)
        {
            if (sorted[i - 1].AddDays(-1) == sorted[i])
                streak++;
            else
                break;
        }
        return streak;
    }

    private static AchievementDto MapAchievement(Achievement a, DateTime? unlockedAt)
    {
        return new AchievementDto
        {
            Id = a.Id,
            Code = a.Code,
            Name = a.NameRu,
            Description = a.DescriptionRu,
            IconPath = a.IconPath,
            SortOrder = a.SortOrder,
            UnlockedAt = unlockedAt
        };
    }
}
