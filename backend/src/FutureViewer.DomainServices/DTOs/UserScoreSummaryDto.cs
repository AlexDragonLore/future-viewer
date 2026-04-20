namespace FutureViewer.DomainServices.DTOs;

public sealed class UserScoreSummaryDto
{
    public required int TotalScore { get; init; }
    public required int FeedbackScore { get; init; }
    public required int AchievementScore { get; init; }
    public required int MonthlyScore { get; init; }
    public int? Rank { get; init; }
    public int? MonthlyRank { get; init; }
    public required int FeedbackCount { get; init; }
    public required double AverageScore { get; init; }
}
