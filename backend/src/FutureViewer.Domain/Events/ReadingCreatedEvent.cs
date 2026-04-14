using FutureViewer.Domain.Enums;

namespace FutureViewer.Domain.Events;

public sealed class ReadingCreatedEvent
{
    public required Guid ReadingId { get; init; }
    public Guid? UserId { get; init; }
    public required SpreadType SpreadType { get; init; }
    public required DateTime CreatedAt { get; init; }
}
