using FutureViewer.Domain.Entities;

namespace FutureViewer.DomainServices.Interfaces;

public interface IUserMemoryRepository
{
    Task<IReadOnlyList<UserMemoryRule>> GetByUserAsync(Guid userId, int take = 20, CancellationToken ct = default);
    Task<UserMemoryRule> AddAsync(UserMemoryRule rule, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken ct = default);
    Task DeleteAllAsync(Guid userId, CancellationToken ct = default);
    Task DeleteOldestBeyondLimitAsync(Guid userId, int limit, CancellationToken ct = default);
}
