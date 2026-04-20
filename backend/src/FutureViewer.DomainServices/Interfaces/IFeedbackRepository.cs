using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;

namespace FutureViewer.DomainServices.Interfaces;

public interface IFeedbackRepository
{
    Task<ReadingFeedback> AddAsync(ReadingFeedback feedback, CancellationToken ct = default);
    Task<ReadingFeedback?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ReadingFeedback?> GetByTokenAsync(string token, CancellationToken ct = default);
    Task<ReadingFeedback?> GetByReadingIdAsync(Guid readingId, CancellationToken ct = default);
    Task<IReadOnlyList<ReadingFeedback>> GetPendingToNotifyAsync(DateTime before, int batch, CancellationToken ct = default);
    Task<IReadOnlyList<ReadingFeedback>> GetScoredByUserAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<ReadingFeedback>> GetByUserAsync(Guid userId, int take = 50, CancellationToken ct = default);
    Task<IReadOnlyList<ReadingFeedback>> SearchAsync(Guid? userId, FeedbackStatus? status, int skip, int take, CancellationToken ct = default);
    Task<int> CountAsync(Guid? userId, FeedbackStatus? status, CancellationToken ct = default);
    Task<int> CountPendingToNotifyAsync(DateTime before, CancellationToken ct = default);
    Task<int> CountScoredSinceAsync(DateTime fromUtc, CancellationToken ct = default);
    Task UpdateAsync(ReadingFeedback feedback, CancellationToken ct = default);
    Task<bool> MarkNotifiedAsync(Guid feedbackId, DateTime notifiedAt, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
