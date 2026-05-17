using FutureViewer.DomainServices.DTOs;

namespace FutureViewer.DomainServices.Interfaces;

public interface IPaymentProvider
{
    Task<PaymentCreationResult> CreatePaymentAsync(
        PaymentCreateRequest request,
        CancellationToken ct = default);

    Task<PaymentCreationResult> CreateSubscriptionPaymentAsync(
        Guid userId,
        string userEmail,
        CancellationToken ct = default);

    PaymentWebhookEvent? ParseWebhook(string body);

    Task<PaymentVerification?> VerifyPaymentAsync(string paymentId, CancellationToken ct = default);
}

public sealed class PaymentCreateRequest
{
    public required Guid UserId { get; init; }
    public required string UserEmail { get; init; }
    public required PaymentProductType ProductType { get; init; }
    public required decimal AmountRub { get; init; }
    public required string Description { get; init; }
    public Guid? TarotPlusSessionId { get; init; }
    public string? ReturnPath { get; init; }
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
    public PaymentProductType ProductType { get; init; } = PaymentProductType.Subscription;
    public Guid? TarotPlusSessionId { get; init; }
}

public sealed class PaymentVerification
{
    public required string PaymentId { get; init; }
    public required string Status { get; init; }
    public required bool Paid { get; init; }
    public Guid? UserId { get; init; }
    public PaymentProductType ProductType { get; init; } = PaymentProductType.Subscription;
    public Guid? TarotPlusSessionId { get; init; }
}
