using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Interfaces;

namespace FutureViewer.DomainServices.Services;

public sealed class LeaderboardService
{
    private const int DefaultTake = 50;

    private readonly ILeaderboardRepository _repo;

    public LeaderboardService(ILeaderboardRepository repo)
    {
        _repo = repo;
    }

    public Task<IReadOnlyList<LeaderboardEntryDto>> GetMonthlyAsync(
        int? year = null,
        int? month = null,
        int take = DefaultTake,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var y = year ?? now.Year;
        var m = month ?? now.Month;
        return _repo.GetMonthlyAsync(y, m, Math.Clamp(take, 1, 200), ct);
    }

    public Task<IReadOnlyList<LeaderboardEntryDto>> GetAllTimeAsync(int take = DefaultTake, CancellationToken ct = default)
    {
        return _repo.GetAllTimeAsync(Math.Clamp(take, 1, 200), ct);
    }

    public Task<UserScoreSummaryDto?> GetUserSummaryAsync(Guid userId, CancellationToken ct = default)
    {
        return _repo.GetUserSummaryAsync(userId, ct);
    }
}
