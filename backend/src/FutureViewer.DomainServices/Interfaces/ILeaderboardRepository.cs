using FutureViewer.DomainServices.DTOs;

namespace FutureViewer.DomainServices.Interfaces;

public interface ILeaderboardRepository
{
    Task<IReadOnlyList<LeaderboardEntryDto>> GetMonthlyAsync(int year, int month, int take, CancellationToken ct = default);
    Task<IReadOnlyList<LeaderboardEntryDto>> GetAllTimeAsync(int take, CancellationToken ct = default);
    Task<UserScoreSummaryDto?> GetUserSummaryAsync(Guid userId, CancellationToken ct = default);
}
