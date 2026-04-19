using FutureViewer.Domain.Entities;

namespace FutureViewer.DomainServices.Interfaces;

public interface IReadingRepository
{
    Task<Reading> AddAsync(Reading reading, CancellationToken ct = default);
    Task<Reading?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Reading>> GetHistoryAsync(Guid userId, int take = 50, CancellationToken ct = default);
    Task UpdateAsync(Reading reading, CancellationToken ct = default);
    Task<int> CountTodayByUserAsync(Guid userId, CancellationToken ct = default);
}
