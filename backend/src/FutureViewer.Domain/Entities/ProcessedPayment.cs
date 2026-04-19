namespace FutureViewer.Domain.Entities;

public sealed class ProcessedPayment
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string PaymentId { get; init; }
    public required Guid UserId { get; init; }
    public DateTime ProcessedAt { get; init; } = DateTime.UtcNow;
}
