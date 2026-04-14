namespace FutureViewer.Domain.Events;

public sealed record ReadingCompletedEvent(Guid ReadingId, string InterpretationPreview, DateTime CompletedAt);
