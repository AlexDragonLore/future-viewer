namespace FutureViewer.Domain.Events;

public sealed class ReadingCompletedEvent
{
    public required Guid ReadingId { get; init; }
    public required string InterpretationPreview { get; init; }
    public required DateTime CompletedAt { get; init; }
}
