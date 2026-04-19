using FutureViewer.Domain.Enums;

namespace FutureViewer.DomainServices.DTOs;

public sealed class FeedbackDto
{
    public required Guid Id { get; init; }
    public required Guid ReadingId { get; init; }
    public required string Question { get; init; }
    public string? Interpretation { get; init; }
    public int? AiScore { get; init; }
    public string? AiScoreReason { get; init; }
    public bool? IsSincere { get; init; }
    public string? SelfReport { get; init; }
    public required FeedbackStatus Status { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? AnsweredAt { get; init; }
}
