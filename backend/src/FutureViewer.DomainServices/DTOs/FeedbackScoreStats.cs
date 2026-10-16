namespace FutureViewer.DomainServices.DTOs;

public sealed class FeedbackScoreStats
{
    public int ScoredCount { get; init; }
    public int Perfect10Count { get; init; }
    public double AverageScore { get; init; }
}
