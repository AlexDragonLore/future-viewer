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
        public const string TelegramLinked = "telegram_linked";
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

    public async Task<IReadOnlyList<AchievementDto>> CheckAndGrantAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("User not found");

        var readingCount = await _readings.CountByUserAsync(userId, ct);
        var scoredFeedbacks = await _feedbacks.GetScoredByUserAsync(userId, ct);
        var scoredCount = scoredFeedbacks.Count;
        var perfect10Count = scoredFeedbacks.Count(f => f.AiScore == 10);
        var averageScore = scoredCount > 0 ? scoredFeedbacks.Average(f => f.AiScore ?? 0) : 0d;

        var maxStreak = 0;
        if (readingCount > 0)
        {
            var fromUtc = DateTime.UtcNow.Date.AddDays(-40);
            var dates = await _readings.GetDistinctReadingDatesAsync(userId, fromUtc, ct);
            maxStreak = ComputeCurrentStreak(dates);
        }

        var telegramLinked = user.TelegramChatId.HasValue;

        var candidateCodes = new List<string>();
        if (readingCount >= 1) candidateCodes.Add(Codes.FirstReading);
        if (readingCount >= 10) candidateCodes.Add(Codes.Total10);
        if (readingCount >= 50) candidateCodes.Add(Codes.Total50);
        if (readingCount >= 100) candidateCodes.Add(Codes.Total100);
        if (scoredCount >= 1) candidateCodes.Add(Codes.FirstFeedback);
        if (telegramLinked) candidateCodes.Add(Codes.TelegramLinked);
        if (maxStreak >= 3) candidateCodes.Add(Codes.Streak3);
        if (maxStreak >= 7) candidateCodes.Add(Codes.Streak7);
        if (maxStreak >= 30) candidateCodes.Add(Codes.Streak30);
        if (perfect10Count >= 1) candidateCodes.Add(Codes.Perfect10);
        if (perfect10Count >= 5) candidateCodes.Add(Codes.HighFive);
        if (scoredCount >= 10 && averageScore >= 8d) candidateCodes.Add(Codes.ScoreMaster);

        if (candidateCodes.Count == 0)
            return Array.Empty<AchievementDto>();

        var all = await _achievements.GetAllAsync(ct);
        var byCode = all.ToDictionary(a => a.Code, a => a);
        var existing = await _achievements.GetByUserAsync(userId, ct);
        var existingAchievementIds = existing.Select(ua => ua.AchievementId).ToHashSet();

        var newlyGranted = new List<AchievementDto>();
        foreach (var code in candidateCodes.Distinct())
        {
            if (!byCode.TryGetValue(code, out var achievement)) continue;
            if (existingAchievementIds.Contains(achievement.Id)) continue;

            var ua = await _achievements.GrantAsync(new UserAchievement
            {
                UserId = userId,
                AchievementId = achievement.Id,
                UnlockedAt = DateTime.UtcNow
            }, ct);

            newlyGranted.Add(MapAchievement(achievement, ua.UnlockedAt));
        }

        return newlyGranted;
    }

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
