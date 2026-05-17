using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Interfaces;

namespace FutureViewer.DomainServices.Services;

public sealed class PaymentWebhookService
{
    private readonly IPaymentProvider _payments;
    private readonly IProcessedPaymentRepository _processedPayments;
    private readonly IUnitOfWork _uow;
    private readonly SubscriptionService _subscriptions;
    private readonly TarotPlusService _tarotPlus;

    public PaymentWebhookService(
        IPaymentProvider payments,
        IProcessedPaymentRepository processedPayments,
        IUnitOfWork uow,
        SubscriptionService subscriptions,
        TarotPlusService tarotPlus)
    {
        _payments = payments;
        _processedPayments = processedPayments;
        _uow = uow;
        _subscriptions = subscriptions;
        _tarotPlus = tarotPlus;
    }

    public async Task<bool> ProcessWebhookAsync(string body, CancellationToken ct = default)
    {
        var evt = _payments.ParseWebhook(body);
        if (evt is null) return false;
        if (evt.Type != PaymentWebhookEventType.PaymentSucceeded) return false;
        if (string.IsNullOrWhiteSpace(evt.PaymentId)) return false;

        var verified = await _payments.VerifyPaymentAsync(evt.PaymentId, ct);
        if (verified is null) return false;
        if (!verified.Paid) return false;
        if (!string.Equals(verified.Status, "succeeded", StringComparison.OrdinalIgnoreCase)) return false;
        if (verified.UserId is null) return false;
        if (verified.ProductType == PaymentProductType.TarotPlusSession
            && verified.TarotPlusSessionId is null)
            return false;

        try
        {
            return await _uow.ExecuteInTransactionAsync(async innerCt =>
            {
                if (!await _processedPayments.TryRecordAsync(verified.PaymentId, verified.UserId.Value, innerCt))
                    return false;

                var processed = verified.ProductType switch
                {
                    PaymentProductType.TarotPlusSession =>
                        await _tarotPlus.ProcessPaymentSucceededAsync(verified, evt, innerCt),
                    _ => await _subscriptions.ProcessPaymentSucceededAsync(verified, innerCt)
                };

                if (!processed)
                    throw new PaymentWebhookRejectedException();

                return true;
            }, ct);
        }
        catch (PaymentWebhookRejectedException)
        {
            return false;
        }
    }

    private sealed class PaymentWebhookRejectedException : Exception
    {
    }
}
