using FutureViewer.Domain.Enums;

namespace FutureViewer.DomainServices.DTOs.Admin;

public sealed class AdminSubscriptionDto
{
    public required SubscriptionStatus Status { get; init; }
    public required bool IsActive { get; init; }
    public DateTime? ExpiresAt { get; init; }
    // Purchase evidence is separate from current access, which may be granted manually.
    public AdminSubscriptionPurchaseDto? LastPurchase { get; set; }
}

public sealed class AdminSubscriptionPurchaseDto
{
    public required string TariffCode { get; init; }
    public required int AccessDays { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required DateTime CompletedAt { get; init; }
}
