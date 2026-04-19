using FutureViewer.Domain.Enums;

namespace FutureViewer.Domain.Entities;

public sealed class ReadingFeedback
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid ReadingId { get; init; }
    public Reading? Reading { get; init; }
    public required Guid UserId { get; init; }
    public User? User { get; init; }
    public required string Token { get; init; }
    public string? SelfReport { get; set; }
    public int? AiScore { get; set; }
    public string? AiScoreReason { get; set; }
    public bool? IsSincere { get; set; }
    public required DateTime ScheduledAt { get; init; }
    public DateTime? NotifiedAt { get; set; }
    public DateTime? AnsweredAt { get; set; }
    public FeedbackStatus Status { get; set; } = FeedbackStatus.Pending;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
