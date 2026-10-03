using FutureViewer.Domain.Enums;

namespace FutureViewer.Domain.Entities;

public sealed class DataDeletionJob
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid? UserId { get; set; }
    public required Guid SubjectReference { get; init; }
    public required DateTime RequestedAt { get; init; }
    public required DateTime ScheduledAt { get; init; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DataDeletionJobStatus Status { get; set; } = DataDeletionJobStatus.Pending;
    public string? FailureReason { get; set; }
}
