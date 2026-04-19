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
}
