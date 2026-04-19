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
}
