namespace FutureViewer.DomainServices.Interfaces;

public interface IPaymentProvider
{
    Task<PaymentCreationResult> CreateSubscriptionPaymentAsync(
        Guid userId,
        string userEmail,
        CancellationToken ct = default);

    PaymentWebhookEvent? ParseWebhook(string body);

    Task<PaymentVerification?> VerifyPaymentAsync(string paymentId, CancellationToken ct = default);
}

public sealed class PaymentCreationResult
{
    public required string PaymentId { get; init; }
    public required string ConfirmationUrl { get; init; }
    public required string Status { get; init; }
}

public enum PaymentWebhookEventType
{
    Unknown = 0,
    PaymentSucceeded = 1,
    PaymentCanceled = 2
}

public sealed class PaymentWebhookEvent
{
    public required PaymentWebhookEventType Type { get; init; }
    public required string PaymentId { get; init; }
    public Guid? UserId { get; init; }
}

public sealed class PaymentVerification
{
    public required string PaymentId { get; init; }
    public required string Status { get; init; }
    public required bool Paid { get; init; }
    public Guid? UserId { get; init; }
}
