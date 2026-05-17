using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FutureViewer.Infrastructure.Persistence.Repositories;

public sealed class TarotPlusSessionRepository : ITarotPlusSessionRepository
{
    private readonly AppDbContext _db;

    public TarotPlusSessionRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<TarotPlusSession> AddAsync(TarotPlusSession session, CancellationToken ct = default)
    {
        await _db.TarotPlusSessions.AddAsync(session, ct);
        await _db.SaveChangesAsync(ct);
        return session;
    }

    public Task<TarotPlusSession?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return _db.TarotPlusSessions.FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public Task<TarotPlusSession?> GetByPaymentIdAsync(string paymentId, CancellationToken ct = default)
    {
        return _db.TarotPlusSessions.FirstOrDefaultAsync(x => x.PaymentId == paymentId, ct);
    }

    public async Task<IReadOnlyList<TarotPlusSession>> GetHistoryAsync(
        Guid userId,
        int take = 20,
        CancellationToken ct = default)
    {
        return await _db.TarotPlusSessions
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TarotPlusSession>> SearchAsync(
        Guid? userId,
        TarotPlusSessionStatus? status,
        int skip,
        int take,
        CancellationToken ct = default)
    {
        return await Filter(userId, status)
            .Include(x => x.User)
            .OrderByDescending(x => x.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    public Task<int> CountAsync(
        Guid? userId = null,
        TarotPlusSessionStatus? status = null,
        CancellationToken ct = default) =>
        Filter(userId, status).CountAsync(ct);

    public Task<int> CountPaidOrLaterAsync(CancellationToken ct = default) =>
        _db.TarotPlusSessions.CountAsync(
            x => x.Status == TarotPlusSessionStatus.Paid
                 || x.Status == TarotPlusSessionStatus.Intake
                 || x.Status == TarotPlusSessionStatus.CardsDrawn
                 || x.Status == TarotPlusSessionStatus.ReportGenerating
                 || x.Status == TarotPlusSessionStatus.ReportReady
                 || x.Status == TarotPlusSessionStatus.Completed,
            ct);

    public Task<int> CountReportsReadyAsync(CancellationToken ct = default) =>
        _db.TarotPlusSessions.CountAsync(
            x => x.Status == TarotPlusSessionStatus.ReportReady
                 || x.Status == TarotPlusSessionStatus.Completed,
            ct);

    public Task<int> CountSinceAsync(DateTime sinceUtc, CancellationToken ct = default) =>
        _db.TarotPlusSessions.CountAsync(x => x.CreatedAt >= sinceUtc, ct);

    public async Task UpdateAsync(TarotPlusSession session, CancellationToken ct = default)
    {
        if (_db.Entry(session).State == EntityState.Detached)
            _db.TarotPlusSessions.Update(session);
        await _db.SaveChangesAsync(ct);
    }

    private IQueryable<TarotPlusSession> Filter(Guid? userId, TarotPlusSessionStatus? status)
    {
        var query = _db.TarotPlusSessions.AsQueryable();
        if (userId.HasValue)
            query = query.Where(x => x.UserId == userId.Value);
        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);
        return query;
    }
}
