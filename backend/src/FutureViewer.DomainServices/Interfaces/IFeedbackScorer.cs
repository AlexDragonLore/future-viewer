using FutureViewer.DomainServices.DTOs;

namespace FutureViewer.DomainServices.Interfaces;

public interface IFeedbackScorer
{
    Task<FeedbackScoringResult> ScoreAsync(
        string question,
        string interpretation,
        string selfReport,
        CancellationToken ct = default);
}
