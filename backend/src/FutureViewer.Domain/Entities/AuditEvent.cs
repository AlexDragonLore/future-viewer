namespace FutureViewer.Domain.Entities;

public sealed class AuditEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required DateTime OccurredAt { get; init; }
    public required string EventType { get; init; }
    public required string CorrelationId { get; init; }
    public Guid? ActorSubjectReference { get; init; }
    public required string TargetType { get; init; }
    public Guid? TargetReference { get; init; }
    public required string Outcome { get; init; }
    public string? ReasonCode { get; init; }
    public string? DocumentVersion { get; init; }
}
