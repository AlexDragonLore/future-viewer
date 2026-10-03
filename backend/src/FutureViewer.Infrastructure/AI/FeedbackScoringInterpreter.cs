using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Interfaces;

namespace FutureViewer.Infrastructure.AI;

/// <summary>
/// Compatibility adapter retained for deployments that referenced the old type.
/// Feedback scoring is now deterministic and local; no feedback content crosses an
/// external boundary.
/// </summary>
[Obsolete("Use PrivacyPreservingFeedbackScorer; this adapter is local-only.")]
public sealed class FeedbackScoringInterpreter : IFeedbackScorer
{
    private readonly PrivacyPreservingFeedbackScorer _local = new();

    public Task<FeedbackScoringResult> ScoreAsync(
        string question,
        string interpretation,
        string selfReport,
        CancellationToken ct = default) =>
        _local.ScoreAsync(question, interpretation, selfReport, ct);
}
