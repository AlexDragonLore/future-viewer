using FutureViewer.Domain.Entities;

namespace FutureViewer.DomainServices.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByLinkTokenAsync(string token, CancellationToken ct = default);
    Task<User?> GetByTelegramChatIdAsync(long chatId, CancellationToken ct = default);
    Task<User> AddAsync(User user, CancellationToken ct = default);
    Task UpdateAsync(User user, CancellationToken ct = default);
    Task<IReadOnlyList<User>> SearchAsync(string? email, int skip, int take, CancellationToken ct = default);
    Task<int> CountAsync(string? email, CancellationToken ct = default);
    Task<int> CountAdminsAsync(CancellationToken ct = default);
    Task<int> CountActiveSubscriptionsAsync(DateTime nowUtc, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
