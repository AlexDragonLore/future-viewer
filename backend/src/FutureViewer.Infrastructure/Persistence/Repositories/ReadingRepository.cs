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
            .Where(r => r.UserId == userId && r.SavedToHistory && r.DeletedFromHistoryAt == null)
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

    public async Task<IReadOnlyList<Reading>> SearchForAdminAsync(
        Guid? userId, string? search, int skip, int take, CancellationToken ct = default) =>
        await AdminReadingsQuery(userId, search)
            .Include(r => r.User)
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

    public Task<int> CountForAdminAsync(Guid? userId, string? search, CancellationToken ct = default) =>
        AdminReadingsQuery(userId, search).CountAsync(ct);

    private IQueryable<Reading> AdminReadingsQuery(Guid? userId, string? search)
    {
        // Retained content stays available to admins after removal from user history.
        // Anonymous previews and older minimized operational rows have no saved content.
        var query = _db.Readings.AsNoTracking().Where(r => r.UserId != null && r.SavedToHistory);
        if (userId.HasValue) query = query.Where(r => r.UserId == userId.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var escaped = search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
            var pattern = $"%{escaped}%";
            query = query.Where(r => EF.Functions.ILike(r.Question, pattern, "\\")
                                     || (r.User != null && EF.Functions.ILike(r.User.Email, pattern, "\\")));
        }
        return query;
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

    public async Task<bool> AttachGuestAsync(
        Guid id, Guid userId, string question, string? interpretation, CancellationToken ct = default)
    {
        // Claim and store the protected ticket together. The owner may retry, but a
        // second account cannot claim it and removing it from history is permanent.
        var updated = await _db.Readings
            .Where(r => r.Id == id && ((r.UserId == null && !r.SavedToHistory) || r.UserId == userId)
                        && r.DeletedFromHistoryAt == null)
            .ExecuteUpdateAsync(update => update
                .SetProperty(r => r.UserId, userId)
                .SetProperty(r => r.Question, question)
                .SetProperty(r => r.AiInterpretation, interpretation)
                .SetProperty(r => r.SavedToHistory, true), ct);
        return updated == 1;
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
