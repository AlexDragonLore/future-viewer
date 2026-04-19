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

        var raw = await _db.ReadingFeedbacks
            .Where(f => f.Status == FeedbackStatus.Scored
                        && f.AiScore != null
                        && f.AnsweredAt != null
                        && f.AnsweredAt >= from
                        && f.AnsweredAt < to)
            .GroupBy(f => f.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                TotalScore = g.Sum(x => x.AiScore!.Value),
                FeedbackCount = g.Count(),
                AverageScore = g.Average(x => (double)x.AiScore!.Value)
            })
            .OrderByDescending(x => x.TotalScore)
            .ThenByDescending(x => x.AverageScore)
            .Take(take)
            .ToListAsync(ct);

        var userIds = raw.Select(r => r.UserId).ToList();
        var emailMap = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email })
            .ToDictionaryAsync(u => u.Id, u => u.Email, ct);

        return raw
            .Select((r, idx) => new LeaderboardEntryDto
            {
                UserId = r.UserId,
                DisplayName = MaskEmail(emailMap.TryGetValue(r.UserId, out var e) ? e : string.Empty),
                TotalScore = r.TotalScore,
                FeedbackCount = r.FeedbackCount,
                AverageScore = Math.Round(r.AverageScore, 2),
                Rank = idx + 1
            })
            .ToList();
    }

    public async Task<IReadOnlyList<LeaderboardEntryDto>> GetAllTimeAsync(int take, CancellationToken ct = default)
    {
        var raw = await _db.ReadingFeedbacks
            .Where(f => f.Status == FeedbackStatus.Scored && f.AiScore != null)
            .GroupBy(f => f.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                TotalScore = g.Sum(x => x.AiScore!.Value),
                FeedbackCount = g.Count(),
                AverageScore = g.Average(x => (double)x.AiScore!.Value)
            })
            .OrderByDescending(x => x.TotalScore)
            .ThenByDescending(x => x.AverageScore)
            .Take(take)
            .ToListAsync(ct);

        var userIds = raw.Select(r => r.UserId).ToList();
        var emailMap = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email })
            .ToDictionaryAsync(u => u.Id, u => u.Email, ct);

        return raw
            .Select((r, idx) => new LeaderboardEntryDto
            {
                UserId = r.UserId,
                DisplayName = MaskEmail(emailMap.TryGetValue(r.UserId, out var e) ? e : string.Empty),
                TotalScore = r.TotalScore,
                FeedbackCount = r.FeedbackCount,
                AverageScore = Math.Round(r.AverageScore, 2),
                Rank = idx + 1
            })
            .ToList();
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

        var userMonthly = await allScored
            .Where(f => f.UserId == userId
                        && f.AnsweredAt != null
                        && f.AnsweredAt >= monthFrom
                        && f.AnsweredAt < monthTo)
            .GroupBy(f => f.UserId)
            .Select(g => new { TotalScore = g.Sum(x => x.AiScore!.Value) })
            .FirstOrDefaultAsync(ct);

        int totalScore = userAll?.TotalScore ?? 0;
        int feedbackCount = userAll?.FeedbackCount ?? 0;
        double averageScore = userAll is null ? 0 : Math.Round(userAll.AverageScore, 2);
        int monthlyScore = userMonthly?.TotalScore ?? 0;

        int? rank = null;
        if (feedbackCount > 0)
        {
            var higher = await allScored
                .GroupBy(f => f.UserId)
                .Select(g => new { UserId = g.Key, TotalScore = g.Sum(x => x.AiScore!.Value) })
                .CountAsync(x => x.TotalScore > totalScore, ct);
            rank = higher + 1;
        }

        int? monthlyRank = null;
        if (monthlyScore > 0)
        {
            var higherMonthly = await allScored
                .Where(f => f.AnsweredAt != null && f.AnsweredAt >= monthFrom && f.AnsweredAt < monthTo)
                .GroupBy(f => f.UserId)
                .Select(g => new { UserId = g.Key, TotalScore = g.Sum(x => x.AiScore!.Value) })
                .CountAsync(x => x.TotalScore > monthlyScore, ct);
            monthlyRank = higherMonthly + 1;
        }

        return new UserScoreSummaryDto
        {
            TotalScore = totalScore,
            MonthlyScore = monthlyScore,
            Rank = rank,
            MonthlyRank = monthlyRank,
            FeedbackCount = feedbackCount,
            AverageScore = averageScore
        };
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
}
