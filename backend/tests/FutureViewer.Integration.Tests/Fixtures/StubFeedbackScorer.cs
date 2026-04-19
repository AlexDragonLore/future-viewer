using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Interfaces;

namespace FutureViewer.Integration.Tests.Fixtures;

public sealed class StubFeedbackScorer : IFeedbackScorer
{
    public Task<FeedbackScoringResult> ScoreAsync(
        string question,
        string interpretation,
        string selfReport,
        CancellationToken ct = default)
    {
        return Task.FromResult(new FeedbackScoringResult
        {
            Score = 8,
            Reason = "Stub scoring reason",
            IsSincere = true
        });
    }
}
