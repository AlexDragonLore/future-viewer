using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using FutureViewer.DomainServices.Interfaces;
using FutureViewer.Infrastructure.Compliance;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FutureViewer.Infrastructure.Payment;

public sealed class YooMoneyRedirectPaymentProvider : IPaymentProvider
{
    private readonly ConcurrentDictionary<string, PaymentVerification> _verifiedPayments = new();
    private readonly YooMoneyOptions _options;
    private readonly ILogger<YooMoneyRedirectPaymentProvider> _logger;

    public YooMoneyRedirectPaymentProvider(
        IOptions<YooMoneyOptions> options,
        ILogger<YooMoneyRedirectPaymentProvider> logger,
        IProcessorRegistryGuard? registry = null)
    {
        _options = options.Value;
        _logger = logger;
    }

    public string ProviderName => "YooMoney";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.Receiver)
        && !string.IsNullOrWhiteSpace(_options.NotificationSecret)
        && PaymentEndpointSafety.IsYooMoneyCheckout(_options.QuickpayUrl)
        && _options.WeeklyPriceAmount > 0 && decimal.Round(_options.WeeklyPriceAmount, 2) == _options.WeeklyPriceAmount
        && _options.MonthlyPriceAmount > 0 && decimal.Round(_options.MonthlyPriceAmount, 2) == _options.MonthlyPriceAmount
        && _options.CurrencyCode == "643";

    public PaymentProductDescriptor Product => new()
    {
        TariffCode = "pro-30d",
        Amount = _options.MonthlyPriceAmount,
        Currency = "RUB",
        AccessDays = 30
    };

    public IReadOnlyList<PaymentProductDescriptor> Products =>
    [
        new()
        {
            TariffCode = "pro-7d",
            Amount = _options.WeeklyPriceAmount,
            Currency = "RUB",
            AccessDays = 7
        },
        Product
    ];

    public Task<PaymentCreationResult> CreateSubscriptionPaymentAsync(
        Guid publicOrderId,
        string idempotencyKey,
        PaymentProductDescriptor product,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Receiver))
            throw new InvalidOperationException("YooMoney receiver is not configured");
        PaymentEndpointSafety.EnsureYooMoneyCheckout(_options.QuickpayUrl);

        _ = idempotencyKey; // YooMoney quickpay has no request idempotency header.
        var label = CreateLabel(publicOrderId);
        var url = BuildQuickpayUrl(label, product.Amount);

        return Task.FromResult(new PaymentCreationResult
        {
            PaymentId = null,
            ConfirmationUrl = url,
            Status = "pending"
        });
    }

    public PaymentWebhookEvent? ParseWebhook(string body)
    {
        PaymentEndpointSafety.EnsureYooMoneyCheckout(_options.QuickpayUrl);
        var form = ParseFormBody(body);
        if (form.Count == 0) return null;

        if (!VerifySignature(form))
            return null;

        if (!form.TryGetValue("operation_id", out var operationId)
            || string.IsNullOrWhiteSpace(operationId))
        {
            _logger.LogWarning("YooMoney notification rejected: operation_id is missing");
            return null;
        }

        if (!form.TryGetValue("label", out var label)
            || TryParseOrderId(label) is not { } orderId)
        {
            _logger.LogWarning("YooMoney notification {OperationId} rejected: label is missing or invalid", operationId);
            return null;
        }

        if (!IsKnownIncomingNotification(form))
        {
            _logger.LogWarning("YooMoney notification {OperationId} rejected: unsupported notification type", operationId);
            return null;
        }

        if (IsTrue(form.GetValueOrDefault("codepro")) || IsTrue(form.GetValueOrDefault("unaccepted")))
        {
            _logger.LogWarning("YooMoney notification {OperationId} rejected: protected or unaccepted transfer", operationId);
            return null;
        }

        if (!string.Equals(form.GetValueOrDefault("currency"), _options.CurrencyCode, StringComparison.Ordinal))
        {
            _logger.LogWarning("YooMoney notification {OperationId} rejected: currency mismatch", operationId);
            return null;
        }

        // The service checks this authenticated amount against the persisted order.
        // Comparing to today's price would reject a payment started before repricing.
        if (!TryGetPaidAmount(form, out var paidAmount) || paidAmount <= 0)
        {
            _logger.LogWarning("YooMoney notification {OperationId} rejected: amount mismatch", operationId);
            return null;
        }

        _verifiedPayments[operationId] = new PaymentVerification
        {
            PaymentId = operationId,
            Status = "succeeded",
            Paid = true,
            UserId = null,
            OrderId = orderId,
            Amount = paidAmount,
            Currency = "RUB"
        };

        return new PaymentWebhookEvent
        {
            Type = PaymentWebhookEventType.PaymentSucceeded,
            PaymentId = operationId,
            UserId = null,
            OrderId = orderId
        };
    }

    public Task<PaymentVerification?> VerifyPaymentAsync(string paymentId, CancellationToken ct = default)
    {
        _verifiedPayments.TryRemove(paymentId, out var verification);
        return Task.FromResult<PaymentVerification?>(verification);
    }

    private string BuildQuickpayUrl(string label, decimal amount)
    {
        var fields = new Dictionary<string, string>
        {
            ["receiver"] = _options.Receiver,
            ["quickpay-form"] = _options.QuickpayForm,
            ["paymentType"] = _options.PaymentType,
            ["sum"] = amount.ToString("F2", CultureInfo.InvariantCulture),
            ["label"] = label,
            ["targets"] = _options.Targets,
            ["successURL"] = _options.ReturnUrl
        };

        var query = string.Join("&", fields
            .Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value)}"));

        return $"{_options.QuickpayUrl}?{query}";
    }

    private bool VerifySignature(IReadOnlyDictionary<string, string> form)
    {
        if (string.IsNullOrWhiteSpace(_options.NotificationSecret))
        {
            _logger.LogWarning("YooMoney notification rejected: notification secret is not configured");
            return false;
        }

        if (!form.TryGetValue("sign", out var sign) || string.IsNullOrWhiteSpace(sign))
        {
            _logger.LogWarning("YooMoney notification rejected: sign is missing");
            return false;
        }

        var canonical = string.Join("&", form
            .Where(x => !string.Equals(x.Key, "sign", StringComparison.Ordinal))
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => $"{x.Key}={Uri.EscapeDataString(x.Value)}"));

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.NotificationSecret));
        var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(sign.ToLowerInvariant()));
    }

    private static Dictionary<string, string> ParseFormBody(string body)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(body)) return result;

        foreach (var part in body.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            var key = WebUtility.UrlDecode(pair[0]);
            if (string.IsNullOrWhiteSpace(key)) continue;

            if (!result.TryAdd(key, pair.Length == 2 ? WebUtility.UrlDecode(pair[1]) : string.Empty))
                return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        return result;
    }

    private static bool IsKnownIncomingNotification(IReadOnlyDictionary<string, string> form)
    {
        var type = form.GetValueOrDefault("notification_type");
        return string.Equals(type, "p2p-incoming", StringComparison.Ordinal)
               || string.Equals(type, "card-incoming", StringComparison.Ordinal);
    }

    private static bool TryGetPaidAmount(IReadOnlyDictionary<string, string> form, out decimal amount)
    {
        var raw = form.GetValueOrDefault("withdraw_amount");
        if (string.IsNullOrWhiteSpace(raw))
            raw = form.GetValueOrDefault("amount");

        return decimal.TryParse(raw, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out amount);
    }

    private static string CreateLabel(Guid publicOrderId)
    {
        var suffix = Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();
        return $"fv-order:{publicOrderId:N}:{suffix}";
    }

    private static Guid? TryParseOrderId(string? label)
    {
        if (string.IsNullOrWhiteSpace(label)) return null;

        var parts = label.Split(':');
        if (parts.Length < 2 || !string.Equals(parts[0], "fv-order", StringComparison.Ordinal))
            return null;

        return Guid.TryParseExact(parts[1], "N", out var id) ? id : null;
    }

    private static bool IsTrue(string? value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
}
