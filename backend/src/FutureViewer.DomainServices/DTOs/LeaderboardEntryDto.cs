namespace FutureViewer.DomainServices.DTOs;

public sealed class LeaderboardEntryDto
{
    public required Guid UserId { get; init; }
    public required string DisplayName { get; init; }
    public required int TotalScore { get; init; }
    public required int FeedbackScore { get; init; }
    public required int AchievementScore { get; init; }
    public required int FeedbackCount { get; init; }
    public required double AverageScore { get; init; }
    public required int Rank { get; init; }
}
