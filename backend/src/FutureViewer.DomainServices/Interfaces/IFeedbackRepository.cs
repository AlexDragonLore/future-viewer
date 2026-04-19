using FutureViewer.Domain.Entities;

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
    Task UpdateAsync(ReadingFeedback feedback, CancellationToken ct = default);
}
