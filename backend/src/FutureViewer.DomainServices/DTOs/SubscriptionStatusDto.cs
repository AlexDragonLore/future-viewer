using FutureViewer.Domain.Enums;

namespace FutureViewer.DomainServices.DTOs;

public sealed class SubscriptionStatusDto
{
    public required SubscriptionStatus Status { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public required bool IsActive { get; init; }
    public required int FreeReadingsUsedToday { get; init; }
    public required int FreeReadingsDailyLimit { get; init; }
    public required bool CanCreateFreeReading { get; init; }
}
