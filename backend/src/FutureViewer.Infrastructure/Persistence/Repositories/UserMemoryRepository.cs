using FutureViewer.Domain.Entities;
using FutureViewer.DomainServices.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FutureViewer.Infrastructure.Persistence.Repositories;

public sealed class UserMemoryRepository : IUserMemoryRepository
{
    private readonly AppDbContext _db;

    public UserMemoryRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<UserMemoryRule>> GetByUserAsync(
        Guid userId,
        int take = 20,
        CancellationToken ct = default)
    {
        return await _db.UserMemoryRules
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.UpdatedAt)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<UserMemoryRule> AddAsync(UserMemoryRule rule, CancellationToken ct = default)
    {
        await _db.UserMemoryRules.AddAsync(rule, ct);
        await _db.SaveChangesAsync(ct);
        return rule;
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var rule = await _db.UserMemoryRules.FirstOrDefaultAsync(r => r.UserId == userId && r.Id == id, ct);
        if (rule is null) return false;

        _db.UserMemoryRules.Remove(rule);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task DeleteAllAsync(Guid userId, CancellationToken ct = default)
    {
        await _db.UserMemoryRules
            .Where(r => r.UserId == userId)
            .ExecuteDeleteAsync(ct);
    }

    public async Task DeleteOldestBeyondLimitAsync(Guid userId, int limit, CancellationToken ct = default)
    {
        var keepIds = await _db.UserMemoryRules
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.UpdatedAt)
            .ThenByDescending(r => r.CreatedAt)
            .Take(limit)
            .Select(r => r.Id)
            .ToListAsync(ct);

        await _db.UserMemoryRules
            .Where(r => r.UserId == userId && !keepIds.Contains(r.Id))
            .ExecuteDeleteAsync(ct);
    }
}
