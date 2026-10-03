namespace FutureViewer.DomainServices.DTOs;

public sealed class LeaderboardEntryDto
{
    public required string EntryId { get; init; }
    public required string DisplayName { get; init; }
    public required int TotalScore { get; init; }
    public required int FeedbackScore { get; init; }
    public required int AchievementScore { get; init; }
    public required int FeedbackCount { get; init; }
    public required double AverageScore { get; init; }
    public required int Rank { get; init; }
}

public static class LeaderboardPublicIdentity
{
    public static string EntryIdForRank(int rank) => $"rank-{ValidateRank(rank)}";

    public static string DisplayNameForRank(int rank) => $"Участник №{ValidateRank(rank)}";

    private static int ValidateRank(int rank) => rank > 0
        ? rank
        : throw new ArgumentOutOfRangeException(nameof(rank));
}
