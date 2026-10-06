using FutureViewer.DomainServices.Interfaces;

namespace FutureViewer.Infrastructure.Payment;

public sealed class DisabledPaymentProvider : IPaymentProvider
{
    public string ProviderName => "disabled";
    public bool IsConfigured => false;
    public IReadOnlyList<PaymentProductDescriptor> Products => [];

    public PaymentProductDescriptor Product => new()
    {
        TariffCode = "disabled",
        Amount = 0,
        Currency = "RUB",
        AccessDays = 0
    };

    public Task<PaymentCreationResult> CreateSubscriptionPaymentAsync(
        Guid publicOrderId,
        string idempotencyKey,
        PaymentProductDescriptor product,
        CancellationToken ct = default) =>
        throw new InvalidOperationException(
            "Payment integration is disabled in the service configuration.");

    public PaymentWebhookEvent? ParseWebhook(string body) => null;

    public Task<PaymentVerification?> VerifyPaymentAsync(
        string paymentId,
        CancellationToken ct = default) => Task.FromResult<PaymentVerification?>(null);
}
