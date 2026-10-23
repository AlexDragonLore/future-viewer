namespace FutureViewer.DomainServices.DTOs.Admin;

public enum AdminPurchaseFilter { All, Paid, Pending, Canceled, Failed, Refunded }

public sealed class AdminPurchaseDto
{
    public required Guid PublicId { get; init; }
    public Guid? UserId { get; init; }
    public string? UserEmail { get; init; }
    public required string TariffCode { get; init; }
    public required int AccessDays { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required string Status { get; init; }
    public required string Provider { get; init; }
    public string? ProviderPaymentId { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
}

public sealed record AdminPurchaseListResult(IReadOnlyList<AdminPurchaseDto> Items, int Total);
