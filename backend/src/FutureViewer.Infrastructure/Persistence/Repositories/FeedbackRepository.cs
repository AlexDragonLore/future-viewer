using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FutureViewer.Infrastructure.Persistence.Repositories;

public sealed class FeedbackRepository : IFeedbackRepository
{
    private readonly AppDbContext _db;

    public FeedbackRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ReadingFeedback> AddAsync(ReadingFeedback feedback, CancellationToken ct = default)
    {
        await _db.ReadingFeedbacks.AddAsync(feedback, ct);
        await _db.SaveChangesAsync(ct);
        return feedback;
    }

    public Task<ReadingFeedback?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.ReadingFeedbacks
            .Include(f => f.Reading)
            .FirstOrDefaultAsync(f => f.Id == id, ct);

    public Task<ReadingFeedback?> GetByTokenAsync(string token, CancellationToken ct = default) =>
        _db.ReadingFeedbacks
            .Include(f => f.Reading)
            .ThenInclude(r => r!.Cards)
            .ThenInclude(c => c.Card)
            .FirstOrDefaultAsync(f => f.Token == token, ct);

    public Task<ReadingFeedback?> GetByReadingIdAsync(Guid readingId, CancellationToken ct = default) =>
        _db.ReadingFeedbacks.FirstOrDefaultAsync(f => f.ReadingId == readingId, ct);

    public async Task<IReadOnlyList<ReadingFeedback>> GetPendingToNotifyAsync(
        DateTime before,
        int batch,
        CancellationToken ct = default)
    {
        return await _db.ReadingFeedbacks
            .Include(f => f.User)
            .Include(f => f.Reading)
            .Where(f => f.Status == FeedbackStatus.Pending
                        && f.ScheduledAt <= before
                        && f.User != null
                        && f.User.TelegramChatId != null)
            .OrderBy(f => f.ScheduledAt)
            .Take(batch)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ReadingFeedback>> GetScoredByUserAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        return await _db.ReadingFeedbacks
            .Where(f => f.UserId == userId && f.Status == FeedbackStatus.Scored && f.AiScore != null)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ReadingFeedback>> GetByUserAsync(
        Guid userId,
        int take = 50,
        CancellationToken ct = default)
    {
        return await _db.ReadingFeedbacks
            .Include(f => f.Reading)
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task UpdateAsync(ReadingFeedback feedback, CancellationToken ct = default)
    {
        if (_db.Entry(feedback).State == EntityState.Detached)
            _db.ReadingFeedbacks.Update(feedback);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> MarkNotifiedAsync(Guid feedbackId, DateTime notifiedAt, CancellationToken ct = default)
    {
        var updated = await _db.ReadingFeedbacks
            .Where(f => f.Id == feedbackId && f.Status == FeedbackStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(f => f.Status, FeedbackStatus.Notified)
                .SetProperty(f => f.NotifiedAt, notifiedAt), ct);
        return updated > 0;
    }

    public async Task<IReadOnlyList<ReadingFeedback>> SearchAsync(
        Guid? userId,
        FeedbackStatus? status,
        int skip,
        int take,
        CancellationToken ct = default)
    {
        var query = _db.ReadingFeedbacks
            .Include(f => f.Reading)
            .Include(f => f.User)
            .AsQueryable();
        if (userId.HasValue) query = query.Where(f => f.UserId == userId.Value);
        if (status.HasValue) query = query.Where(f => f.Status == status.Value);
        return await query
            .OrderByDescending(f => f.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    public Task<int> CountAsync(Guid? userId, FeedbackStatus? status, CancellationToken ct = default)
    {
        var query = _db.ReadingFeedbacks.AsQueryable();
        if (userId.HasValue) query = query.Where(f => f.UserId == userId.Value);
        if (status.HasValue) query = query.Where(f => f.Status == status.Value);
        return query.CountAsync(ct);
    }

    public Task<int> CountPendingToNotifyAsync(DateTime before, CancellationToken ct = default) =>
        _db.ReadingFeedbacks.CountAsync(
            f => f.Status == FeedbackStatus.Pending
                 && f.ScheduledAt <= before
                 && f.User != null
                 && f.User.TelegramChatId != null,
            ct);

    public Task<int> CountScoredSinceAsync(DateTime fromUtc, CancellationToken ct = default) =>
        _db.ReadingFeedbacks.CountAsync(
            f => f.Status == FeedbackStatus.Scored && f.AnsweredAt != null && f.AnsweredAt >= fromUtc,
            ct);

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var tracked = _db.ChangeTracker.Entries<ReadingFeedback>()
            .Where(e => e.Entity.Id == id)
            .ToList();
        foreach (var entry in tracked)
            entry.State = EntityState.Detached;

        var deleted = await _db.ReadingFeedbacks
            .Where(f => f.Id == id)
            .ExecuteDeleteAsync(ct);
        return deleted > 0;
    }
}
