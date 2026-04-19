using FluentValidation;
using FluentValidation.Results;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Interfaces;

namespace FutureViewer.DomainServices.Services;

public sealed class LeaderboardService
{
    private const int DefaultTake = 50;
    private const int MinYear = 2024;
    private const int MaxYear = 2100;

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

        var failures = new List<ValidationFailure>();
        if (y < MinYear || y > MaxYear)
            failures.Add(new ValidationFailure("year", $"year must be between {MinYear} and {MaxYear}"));
        if (m < 1 || m > 12)
            failures.Add(new ValidationFailure("month", "month must be between 1 and 12"));
        if (failures.Count > 0)
            throw new ValidationException(failures);

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
