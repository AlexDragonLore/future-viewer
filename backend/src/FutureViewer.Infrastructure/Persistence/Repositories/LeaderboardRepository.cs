using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FutureViewer.Infrastructure.Persistence.Repositories;

public sealed class LeaderboardRepository : ILeaderboardRepository
{
    private readonly AppDbContext _db;

    public LeaderboardRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<LeaderboardEntryDto>> GetMonthlyAsync(
        int year,
        int month,
        int take,
        CancellationToken ct = default)
    {
        var (from, to) = MonthRange(year, month);

        var feedback = await _db.ReadingFeedbacks
            .Where(f => f.Status == FeedbackStatus.Scored
                        && f.AiScore != null
                        && f.AnsweredAt != null
                        && f.AnsweredAt >= from
                        && f.AnsweredAt < to)
            .GroupBy(f => f.UserId)
            .Select(g => new UserFeedbackAgg(
                g.Key,
                g.Sum(x => x.AiScore!.Value),
                g.Count(),
                g.Average(x => (double)x.AiScore!.Value)))
            .ToListAsync(ct);

        var achievements = await _db.UserAchievements
            .Where(ua => ua.UnlockedAt >= from && ua.UnlockedAt < to)
            .GroupBy(ua => ua.UserId)
            .Select(g => new UserAchievementAgg(
                g.Key,
                g.Sum(x => x.Achievement.Points)))
            .ToListAsync(ct);

        return await CombineAsync(feedback, achievements, take, ct);
    }

    public async Task<IReadOnlyList<LeaderboardEntryDto>> GetAllTimeAsync(int take, CancellationToken ct = default)
    {
        var feedback = await _db.ReadingFeedbacks
            .Where(f => f.Status == FeedbackStatus.Scored && f.AiScore != null)
            .GroupBy(f => f.UserId)
            .Select(g => new UserFeedbackAgg(
                g.Key,
                g.Sum(x => x.AiScore!.Value),
                g.Count(),
                g.Average(x => (double)x.AiScore!.Value)))
            .ToListAsync(ct);

        var achievements = await _db.UserAchievements
            .GroupBy(ua => ua.UserId)
            .Select(g => new UserAchievementAgg(
                g.Key,
                g.Sum(x => x.Achievement.Points)))
            .ToListAsync(ct);

        return await CombineAsync(feedback, achievements, take, ct);
    }

    public async Task<UserScoreSummaryDto?> GetUserSummaryAsync(Guid userId, CancellationToken ct = default)
    {
        var userExists = await _db.Users.AnyAsync(u => u.Id == userId, ct);
        if (!userExists) return null;

        var now = DateTime.UtcNow;
        var (monthFrom, monthTo) = MonthRange(now.Year, now.Month);

        var allScored = _db.ReadingFeedbacks
            .Where(f => f.Status == FeedbackStatus.Scored && f.AiScore != null);

        var userAll = await allScored
            .Where(f => f.UserId == userId)
            .GroupBy(f => f.UserId)
            .Select(g => new
            {
                TotalScore = g.Sum(x => x.AiScore!.Value),
                FeedbackCount = g.Count(),
                AverageScore = g.Average(x => (double)x.AiScore!.Value)
            })
            .FirstOrDefaultAsync(ct);

        var userMonthlyFeedback = await allScored
            .Where(f => f.UserId == userId
                        && f.AnsweredAt != null
                        && f.AnsweredAt >= monthFrom
                        && f.AnsweredAt < monthTo)
            .GroupBy(f => f.UserId)
            .Select(g => new { TotalScore = g.Sum(x => x.AiScore!.Value) })
            .FirstOrDefaultAsync(ct);

        var userAchievementScore = await _db.UserAchievements
            .Where(ua => ua.UserId == userId)
            .SumAsync(ua => (int?)ua.Achievement.Points, ct) ?? 0;

        var userMonthlyAchievementScore = await _db.UserAchievements
            .Where(ua => ua.UserId == userId
                         && ua.UnlockedAt >= monthFrom
                         && ua.UnlockedAt < monthTo)
            .SumAsync(ua => (int?)ua.Achievement.Points, ct) ?? 0;

        int feedbackScore = userAll?.TotalScore ?? 0;
        int feedbackCount = userAll?.FeedbackCount ?? 0;
        double averageScore = userAll is null ? 0 : Math.Round(userAll.AverageScore, 2);
        int monthlyFeedback = userMonthlyFeedback?.TotalScore ?? 0;

        int totalScore = feedbackScore + userAchievementScore;
        int monthlyScore = monthlyFeedback + userMonthlyAchievementScore;

        int? rank = null;
        if (totalScore > 0)
        {
            var allTime = await ComputeAllTimeTotalsAsync(ct);
            rank = allTime.Count(x => x.Total > totalScore) + 1;
        }

        int? monthlyRank = null;
        if (monthlyScore > 0)
        {
            var monthly = await ComputeMonthlyTotalsAsync(monthFrom, monthTo, ct);
            monthlyRank = monthly.Count(x => x.Total > monthlyScore) + 1;
        }

        return new UserScoreSummaryDto
        {
            TotalScore = totalScore,
            FeedbackScore = feedbackScore,
            AchievementScore = userAchievementScore,
            MonthlyScore = monthlyScore,
            Rank = rank,
            MonthlyRank = monthlyRank,
            FeedbackCount = feedbackCount,
            AverageScore = averageScore
        };
    }

    private async Task<IReadOnlyList<LeaderboardEntryDto>> CombineAsync(
        IReadOnlyList<UserFeedbackAgg> feedback,
        IReadOnlyList<UserAchievementAgg> achievements,
        int take,
        CancellationToken ct)
    {
        var feedbackByUser = feedback.ToDictionary(f => f.UserId);
        var achievementByUser = achievements.ToDictionary(a => a.UserId);

        var allUserIds = feedbackByUser.Keys.Union(achievementByUser.Keys).ToList();
        if (allUserIds.Count == 0)
            return Array.Empty<LeaderboardEntryDto>();

        var merged = allUserIds
            .Select(id =>
            {
                feedbackByUser.TryGetValue(id, out var f);
                achievementByUser.TryGetValue(id, out var a);
                int feedbackScore = f?.TotalScore ?? 0;
                int achievementScore = a?.TotalScore ?? 0;
                return new
                {
                    UserId = id,
                    FeedbackScore = feedbackScore,
                    AchievementScore = achievementScore,
                    TotalScore = feedbackScore + achievementScore,
                    FeedbackCount = f?.FeedbackCount ?? 0,
                    AverageScore = f?.AverageScore ?? 0d
                };
            })
            .OrderByDescending(x => x.TotalScore)
            .ThenByDescending(x => x.AverageScore)
            .Take(take)
            .ToList();

        var userIds = merged.Select(r => r.UserId).ToList();
        var emailMap = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email })
            .ToDictionaryAsync(u => u.Id, u => u.Email, ct);

        return merged
            .Select((r, idx) => new LeaderboardEntryDto
            {
                UserId = r.UserId,
                DisplayName = MaskEmail(emailMap.TryGetValue(r.UserId, out var e) ? e : string.Empty),
                TotalScore = r.TotalScore,
                FeedbackScore = r.FeedbackScore,
                AchievementScore = r.AchievementScore,
                FeedbackCount = r.FeedbackCount,
                AverageScore = Math.Round(r.AverageScore, 2),
                Rank = idx + 1
            })
            .ToList();
    }

    private async Task<List<UserTotal>> ComputeAllTimeTotalsAsync(CancellationToken ct)
    {
        var feedback = await _db.ReadingFeedbacks
            .Where(f => f.Status == FeedbackStatus.Scored && f.AiScore != null)
            .GroupBy(f => f.UserId)
            .Select(g => new { UserId = g.Key, Total = g.Sum(x => x.AiScore!.Value) })
            .ToListAsync(ct);

        var achievements = await _db.UserAchievements
            .GroupBy(ua => ua.UserId)
            .Select(g => new { UserId = g.Key, Total = g.Sum(x => x.Achievement.Points) })
            .ToListAsync(ct);

        return MergeTotals(feedback.Select(x => (x.UserId, x.Total)), achievements.Select(x => (x.UserId, x.Total)));
    }

    private async Task<List<UserTotal>> ComputeMonthlyTotalsAsync(DateTime from, DateTime to, CancellationToken ct)
    {
        var feedback = await _db.ReadingFeedbacks
            .Where(f => f.Status == FeedbackStatus.Scored
                        && f.AiScore != null
                        && f.AnsweredAt != null
                        && f.AnsweredAt >= from
                        && f.AnsweredAt < to)
            .GroupBy(f => f.UserId)
            .Select(g => new { UserId = g.Key, Total = g.Sum(x => x.AiScore!.Value) })
            .ToListAsync(ct);

        var achievements = await _db.UserAchievements
            .Where(ua => ua.UnlockedAt >= from && ua.UnlockedAt < to)
            .GroupBy(ua => ua.UserId)
            .Select(g => new { UserId = g.Key, Total = g.Sum(x => x.Achievement.Points) })
            .ToListAsync(ct);

        return MergeTotals(feedback.Select(x => (x.UserId, x.Total)), achievements.Select(x => (x.UserId, x.Total)));
    }

    private static List<UserTotal> MergeTotals(
        IEnumerable<(Guid UserId, int Total)> feedback,
        IEnumerable<(Guid UserId, int Total)> achievements)
    {
        var map = new Dictionary<Guid, int>();
        foreach (var (userId, total) in feedback)
            map[userId] = (map.TryGetValue(userId, out var v) ? v : 0) + total;
        foreach (var (userId, total) in achievements)
            map[userId] = (map.TryGetValue(userId, out var v) ? v : 0) + total;
        return map.Select(kvp => new UserTotal(kvp.Key, kvp.Value)).ToList();
    }

    private static (DateTime from, DateTime to) MonthRange(int year, int month)
    {
        var from = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddMonths(1);
        return (from, to);
    }

    internal static string MaskEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return "***";
        var at = email.IndexOf('@');
        if (at <= 0) return "***";

        var local = email[..at];
        var domain = email[(at + 1)..];

        var localMasked = local.Length switch
        {
            1 => "*",
            2 => local[0] + "*",
            _ => local[0] + new string('*', Math.Min(3, local.Length - 1))
        };

        return $"{localMasked}@{domain}";
    }

    private sealed record UserFeedbackAgg(Guid UserId, int TotalScore, int FeedbackCount, double AverageScore);
    private sealed record UserAchievementAgg(Guid UserId, int TotalScore);
    private sealed record UserTotal(Guid UserId, int Total);
}
