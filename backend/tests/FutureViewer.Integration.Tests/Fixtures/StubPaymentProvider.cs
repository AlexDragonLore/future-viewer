using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Interfaces;

namespace FutureViewer.Integration.Tests.Fixtures;

public sealed class StubPaymentProvider : IPaymentProvider
{
    private readonly Dictionary<string, PaymentVerification> _verifications = new();

    public Task<PaymentCreationResult> CreatePaymentAsync(
        PaymentCreateRequest request,
        CancellationToken ct = default)
    {
        var paymentId = $"stub-{request.ProductType}-{Guid.NewGuid():N}";
        _verifications[paymentId] = new PaymentVerification
        {
            PaymentId = paymentId,
            Status = "succeeded",
            Paid = true,
            UserId = request.UserId,
            ProductType = request.ProductType,
            TarotPlusSessionId = request.TarotPlusSessionId
        };

        return Task.FromResult(new PaymentCreationResult
        {
            PaymentId = paymentId,
            ConfirmationUrl = $"https://pay.example/confirm/{paymentId}",
            Status = "pending"
        });
    }

    public Task<PaymentCreationResult> CreateSubscriptionPaymentAsync(
        Guid userId,
        string userEmail,
        CancellationToken ct = default)
    {
        return CreatePaymentAsync(new PaymentCreateRequest
        {
            UserId = userId,
            UserEmail = userEmail,
            ProductType = PaymentProductType.Subscription,
            AmountRub = 300m,
            Description = "Subscription"
        }, ct);
    }

    public PaymentWebhookEvent? ParseWebhook(string body) => null;

    public Task<PaymentVerification?> VerifyPaymentAsync(string paymentId, CancellationToken ct = default)
    {
        _verifications.TryGetValue(paymentId, out var verification);
        return Task.FromResult<PaymentVerification?>(verification);
    }
}
