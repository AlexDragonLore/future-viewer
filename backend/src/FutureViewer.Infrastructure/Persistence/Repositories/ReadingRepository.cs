using FutureViewer.Domain.Entities;
using FutureViewer.DomainServices.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FutureViewer.Infrastructure.Persistence.Repositories;

public sealed class ReadingRepository : IReadingRepository
{
    private readonly AppDbContext _db;

    public ReadingRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Reading> AddAsync(Reading reading, CancellationToken ct = default)
    {
        foreach (var rc in reading.Cards)
        {
            _db.Entry(rc.Card).State = EntityState.Unchanged;
        }
        await _db.Readings.AddAsync(reading, ct);
        await _db.SaveChangesAsync(ct);
        return reading;
    }

    public async Task<Reading?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Readings
            .Include(r => r.Cards)
            .ThenInclude(c => c.Card)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<IReadOnlyList<Reading>> GetHistoryAsync(Guid userId, int take = 50, CancellationToken ct = default)
    {
        return await _db.Readings
            .Include(r => r.Cards)
            .ThenInclude(c => c.Card)
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Reading>> GetByUserAsync(Guid userId, int take, CancellationToken ct = default)
    {
        return await _db.Readings
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task UpdateAsync(Reading reading, CancellationToken ct = default)
    {
        if (_db.Entry(reading).State == EntityState.Detached)
            _db.Readings.Update(reading);
        await _db.SaveChangesAsync(ct);
    }

    public Task<int> CountTodayByUserAsync(Guid userId, CancellationToken ct = default)
    {
        var todayUtc = DateTime.UtcNow.Date;
        var tomorrowUtc = todayUtc.AddDays(1);
        return _db.Readings
            .Where(r => r.UserId == userId
                        && r.CreatedAt >= todayUtc
                        && r.CreatedAt < tomorrowUtc)
            .CountAsync(ct);
    }

    public Task<int> CountByUserAsync(Guid userId, CancellationToken ct = default)
    {
        return _db.Readings.CountAsync(r => r.UserId == userId, ct);
    }

    public Task<int> CountAsync(CancellationToken ct = default)
    {
        return _db.Readings.CountAsync(ct);
    }

    public Task<int> CountSinceAsync(DateTime fromUtc, CancellationToken ct = default)
    {
        return _db.Readings.CountAsync(r => r.CreatedAt >= fromUtc, ct);
    }

    public async Task<IReadOnlyList<DateTime>> GetDistinctReadingDatesAsync(
        Guid userId,
        DateTime fromUtc,
        CancellationToken ct = default)
    {
        var dates = await _db.Readings
            .Where(r => r.UserId == userId && r.CreatedAt >= fromUtc)
            .Select(r => r.CreatedAt.Date)
            .Distinct()
            .OrderByDescending(d => d)
            .ToListAsync(ct);
        return dates;
    }
}
