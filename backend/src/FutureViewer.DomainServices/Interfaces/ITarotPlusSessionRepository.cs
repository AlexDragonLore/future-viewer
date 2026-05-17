using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;

namespace FutureViewer.DomainServices.Interfaces;

public interface ITarotPlusSessionRepository
{
    Task<TarotPlusSession> AddAsync(TarotPlusSession session, CancellationToken ct = default);
    Task<TarotPlusSession?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<TarotPlusSession?> GetByPaymentIdAsync(string paymentId, CancellationToken ct = default);
    Task<IReadOnlyList<TarotPlusSession>> GetHistoryAsync(Guid userId, int take = 20, CancellationToken ct = default);
    Task<IReadOnlyList<TarotPlusSession>> SearchAsync(
        Guid? userId,
        TarotPlusSessionStatus? status,
        int skip,
        int take,
        CancellationToken ct = default);
    Task<int> CountAsync(Guid? userId = null, TarotPlusSessionStatus? status = null, CancellationToken ct = default);
    Task<int> CountPaidOrLaterAsync(CancellationToken ct = default);
    Task<int> CountReportsReadyAsync(CancellationToken ct = default);
    Task<int> CountSinceAsync(DateTime sinceUtc, CancellationToken ct = default);
    Task UpdateAsync(TarotPlusSession session, CancellationToken ct = default);
}
