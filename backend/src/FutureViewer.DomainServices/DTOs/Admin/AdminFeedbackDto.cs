using FutureViewer.Domain.Enums;

namespace FutureViewer.DomainServices.DTOs.Admin;

public sealed class AdminFeedbackDto
{
    public required Guid Id { get; init; }
    public required Guid ReadingId { get; init; }
    public required Guid UserId { get; init; }
    public string? UserEmail { get; init; }
    public string? Question { get; init; }
    public string? SelfReport { get; init; }
    public int? AiScore { get; init; }
    public string? AiScoreReason { get; init; }
    public bool? IsSincere { get; init; }
    public required DateTime ScheduledAt { get; init; }
    public DateTime? NotifiedAt { get; init; }
    public DateTime? AnsweredAt { get; init; }
    public required FeedbackStatus Status { get; init; }
    public required DateTime CreatedAt { get; init; }
}
