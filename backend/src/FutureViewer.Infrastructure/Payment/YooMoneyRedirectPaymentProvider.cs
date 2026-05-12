using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using FutureViewer.DomainServices.Interfaces;
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
        ILogger<YooMoneyRedirectPaymentProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<PaymentCreationResult> CreateSubscriptionPaymentAsync(
        Guid userId,
        string userEmail,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Receiver))
            throw new InvalidOperationException("YooMoney receiver is not configured");

        var label = CreateLabel(userId);
        var url = BuildQuickpayUrl(label);

        return Task.FromResult(new PaymentCreationResult
        {
            PaymentId = label,
            ConfirmationUrl = url,
            Status = "pending"
        });
    }

    public PaymentWebhookEvent? ParseWebhook(string body)
    {
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
            || TryParseUserId(label) is not { } userId)
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

        if (!TryGetPaidAmount(form, out var paidAmount) || paidAmount < _options.MonthlyPriceAmount)
        {
            _logger.LogWarning("YooMoney notification {OperationId} rejected: amount mismatch", operationId);
            return null;
        }

        _verifiedPayments[operationId] = new PaymentVerification
        {
            PaymentId = operationId,
            Status = "succeeded",
            Paid = true,
            UserId = userId
        };

        return new PaymentWebhookEvent
        {
            Type = PaymentWebhookEventType.PaymentSucceeded,
            PaymentId = operationId,
            UserId = userId
        };
    }

    public Task<PaymentVerification?> VerifyPaymentAsync(string paymentId, CancellationToken ct = default)
    {
        _verifiedPayments.TryGetValue(paymentId, out var verification);
        return Task.FromResult<PaymentVerification?>(verification);
    }

    private string BuildQuickpayUrl(string label)
    {
        var fields = new Dictionary<string, string>
        {
            ["receiver"] = _options.Receiver,
            ["quickpay-form"] = _options.QuickpayForm,
            ["paymentType"] = _options.PaymentType,
            ["sum"] = _options.MonthlyPriceAmount.ToString("F2", CultureInfo.InvariantCulture),
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

            result[key] = pair.Length == 2 ? WebUtility.UrlDecode(pair[1]) : string.Empty;
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

        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
    }

    private static string CreateLabel(Guid userId)
    {
        var suffix = Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();
        return $"fv:{userId:N}:{suffix}";
    }

    private static Guid? TryParseUserId(string? label)
    {
        if (string.IsNullOrWhiteSpace(label)) return null;

        var parts = label.Split(':');
        if (parts.Length < 2 || !string.Equals(parts[0], "fv", StringComparison.Ordinal))
            return null;

        return Guid.TryParseExact(parts[1], "N", out var id) ? id : null;
    }

    private static bool IsTrue(string? value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
}
