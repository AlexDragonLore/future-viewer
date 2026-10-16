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
        return await _db.Achievements.AsNoTracking()
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
        return await _db.UserAchievements.AsNoTracking()
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

    public async Task<IReadOnlyList<UserAchievement>> GrantManyAsync(
        IReadOnlyCollection<UserAchievement> userAchievements, CancellationToken ct = default)
    {
        if (userAchievements.Count == 0) return [];
        var rows = userAchievements.DistinctBy(x => (x.UserId, x.AchievementId)).ToArray();
        var ids = rows.Select(x => x.Id).ToArray();
        var users = rows.Select(x => x.UserId).ToArray();
        var achievements = rows.Select(x => x.AchievementId).ToArray();
        var unlocked = rows.Select(x => x.UnlockedAt).ToArray();
        // A deterministic lock order and the unique constraint make simultaneous
        // checks idempotent; RETURNING reports only this caller's new grants.
        return await _db.UserAchievements.FromSqlInterpolated($"""
            INSERT INTO user_achievements (id, user_id, achievement_id, unlocked_at)
            SELECT id, user_id, achievement_id, unlocked_at
            FROM unnest({ids}, {users}, {achievements}, {unlocked})
                AS grant_rows(id, user_id, achievement_id, unlocked_at)
            ORDER BY user_id, achievement_id
            ON CONFLICT (user_id, achievement_id) DO NOTHING
            RETURNING id, user_id, achievement_id, unlocked_at
            """).AsNoTracking().ToListAsync(ct);
    }
}
