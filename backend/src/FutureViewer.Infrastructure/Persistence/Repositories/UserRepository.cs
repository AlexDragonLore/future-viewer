using FutureViewer.Domain.Entities;
using FutureViewer.DomainServices.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FutureViewer.Infrastructure.Persistence.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;

    public UserRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByLinkTokenAsync(string token, CancellationToken ct = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.TelegramLinkToken == token, ct);

    public Task<User?> GetByTelegramChatIdAsync(long chatId, CancellationToken ct = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.TelegramChatId == chatId, ct);

    public Task<User?> GetByEmailVerificationTokenAsync(string token, CancellationToken ct = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.EmailVerificationToken == token, ct);

    public async Task<User> AddAsync(User user, CancellationToken ct = default)
    {
        await _db.Users.AddAsync(user, ct);
        await _db.SaveChangesAsync(ct);
        return user;
    }

    public async Task UpdateAsync(User user, CancellationToken ct = default)
    {
        if (_db.Entry(user).State == EntityState.Detached)
            _db.Users.Update(user);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<User>> SearchAsync(
        string? email,
        int skip,
        int take,
        CancellationToken ct = default)
    {
        var query = _db.Users.AsQueryable();
        if (!string.IsNullOrWhiteSpace(email))
        {
            var pattern = $"%{email.Trim()}%";
            query = query.Where(u => EF.Functions.ILike(u.Email, pattern));
        }
        return await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    public Task<int> CountAsync(string? email, CancellationToken ct = default)
    {
        var query = _db.Users.AsQueryable();
        if (!string.IsNullOrWhiteSpace(email))
        {
            var pattern = $"%{email.Trim()}%";
            query = query.Where(u => EF.Functions.ILike(u.Email, pattern));
        }
        return query.CountAsync(ct);
    }

    public Task<int> CountAdminsAsync(CancellationToken ct = default) =>
        _db.Users.CountAsync(u => u.IsAdmin, ct);

    public Task<int> CountActiveSubscriptionsAsync(DateTime nowUtc, CancellationToken ct = default) =>
        _db.Users.CountAsync(
            u => u.SubscriptionStatus == Domain.Enums.SubscriptionStatus.Active
                 && (u.SubscriptionExpiresAt == null || u.SubscriptionExpiresAt > nowUtc),
            ct);

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        // Detach tracked entries so a prior lookup in the same scope doesn't conflict
        // with the Remove + SaveChanges path below.
        var tracked = _db.ChangeTracker.Entries<User>()
            .Where(e => e.Entity.Id == id)
            .ToList();
        foreach (var entry in tracked)
            entry.State = EntityState.Detached;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return false;

        _db.Users.Remove(user);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
