namespace FutureViewer.DomainServices.DTOs;

public sealed class FeedbackScoringResult
{
    public required int Score { get; init; }
    public required string Reason { get; init; }
    public required bool IsSincere { get; init; }
}
