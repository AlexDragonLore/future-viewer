using FutureViewer.Domain.Entities;
using FutureViewer.DomainServices.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FutureViewer.Infrastructure.Persistence.Repositories;

public sealed class AchievementRepository : IAchievementRepository
{
    private readonly AppDbContext _db;

    public AchievementRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Achievement>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.Achievements
            .OrderBy(a => a.SortOrder)
            .ToListAsync(ct);
    }

    public async Task<Achievement?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        return await _db.Achievements
            .FirstOrDefaultAsync(a => a.Code == code, ct);
    }

    public async Task<IReadOnlyList<UserAchievement>> GetByUserAsync(Guid userId, CancellationToken ct = default)
    {
        return await _db.UserAchievements
            .Include(ua => ua.Achievement)
            .Where(ua => ua.UserId == userId)
            .OrderByDescending(ua => ua.UnlockedAt)
            .ToListAsync(ct);
    }

    public async Task<UserAchievement?> GrantAsync(UserAchievement userAchievement, CancellationToken ct = default)
    {
        await _db.UserAchievements.AddAsync(userAchievement, ct);
        try
        {
            await _db.SaveChangesAsync(ct);
            return userAchievement;
        }
        catch (DbUpdateException)
        {
            _db.Entry(userAchievement).State = EntityState.Detached;
            return null;
        }
    }

    public async Task<bool> RevokeAsync(Guid userId, Guid achievementId, CancellationToken ct = default)
    {
        var existing = await _db.UserAchievements
            .FirstOrDefaultAsync(ua => ua.UserId == userId && ua.AchievementId == achievementId, ct);
        if (existing is null) return false;

        _db.UserAchievements.Remove(existing);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
