using FutureViewer.Domain.Enums;

namespace FutureViewer.Domain.Events;

public sealed record ReadingCreatedEvent(Guid ReadingId, Guid? UserId, SpreadType SpreadType, DateTime CreatedAt);
