namespace FutureViewer.DomainServices.DTOs.Admin;

public sealed class AdminStatsDto
{
    public required int TotalUsers { get; init; }
    public required int AdminCount { get; init; }
    public required int ActiveSubscriptions { get; init; }
    public required int ReadingsToday { get; init; }
    public required int ReadingsThisWeek { get; init; }
    public required int TarotPlusSessionsTotal { get; init; }
    public required int TarotPlusPaidSessions { get; init; }
    public required int TarotPlusReportsReady { get; init; }
    public required int TarotPlusCreatedThisWeek { get; init; }
    public required int PendingFeedbacksToNotify { get; init; }
    public required int ScoredFeedbacksThisMonth { get; init; }
}
