namespace FutureViewer.DomainServices.Interfaces;

public interface IPaymentProvider
{
    string ProviderName { get; }
    PaymentProductDescriptor Product { get; }
    bool IsConfigured => true;

    Task<PaymentCreationResult> CreateSubscriptionPaymentAsync(
        Guid publicOrderId,
        string idempotencyKey,
        CancellationToken ct = default);

    PaymentWebhookEvent? ParseWebhook(string body);

    bool IsWebhookSourceAllowed(string? sourceAddress) => true;

    Task<PaymentVerification?> VerifyPaymentAsync(string paymentId, CancellationToken ct = default);
}

public sealed class PaymentCreationResult
{
    // Redirect-only providers assign an operation ID only after the transfer.
    // Their order label must never be stored as if it were that operation ID.
    public string? PaymentId { get; init; }
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
    /// <summary>
    /// Random public order identifier. It is deliberately unrelated to a user,
    /// email address, Telegram identifier, or any other personal identifier.
    /// </summary>
    public Guid? OrderId { get; init; }

    // Kept for backwards compatibility with older providers/tests. New payment
    // integrations must resolve the subject only through OrderId and the local DB.
    public Guid? UserId { get; init; }
}

public sealed class PaymentVerification
{
    public required string PaymentId { get; init; }
    public required string Status { get; init; }
    public required bool Paid { get; init; }
    public Guid? UserId { get; init; }
    public Guid? OrderId { get; init; }
    public decimal? Amount { get; init; }
    public string? Currency { get; init; }
}

public sealed class PaymentProductDescriptor
{
    public required string TariffCode { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required int AccessDays { get; init; }
}
