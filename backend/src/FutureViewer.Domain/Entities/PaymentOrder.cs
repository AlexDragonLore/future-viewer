using FutureViewer.Domain.Enums;

namespace FutureViewer.Domain.Entities;

public sealed class PaymentOrder
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid PublicId { get; init; } = Guid.NewGuid();
    public Guid? UserId { get; set; }
    public User? User { get; set; }
    public required Guid SubjectReference { get; init; }
    public required string TariffCode { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required int AccessDays { get; init; }
    public required string Provider { get; init; }
    public required string IdempotencyKey { get; init; }
    public string? ProviderPaymentId { get; set; }
    public PaymentOrderStatus Status { get; set; } = PaymentOrderStatus.Pending;
    public NpdReceiptStatus ReceiptStatus { get; set; } = NpdReceiptStatus.PendingManualIssue;
    public string? ReceiptReference { get; set; }
    public DateTime? ReceiptIssuedAt { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
