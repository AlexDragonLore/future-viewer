using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using FutureViewer.DomainServices.DTOs;
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
        return CreatePaymentAsync(new PaymentCreateRequest
        {
            UserId = userId,
            UserEmail = userEmail,
            ProductType = PaymentProductType.Subscription,
            AmountRub = _options.MonthlyPriceAmount,
            Description = _options.Targets
        }, ct);
    }

    public Task<PaymentCreationResult> CreatePaymentAsync(
        PaymentCreateRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Receiver))
            throw new InvalidOperationException("YooMoney receiver is not configured");
        EnsureExpectedAmount(request);

        var label = CreateLabel(request);
        var url = BuildQuickpayUrl(label, request);

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
            || TryParseLabel(label) is not { } parsedLabel)
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

        var expectedAmount = ExpectedAmount(parsedLabel.ProductType);
        if (!TryGetPaidAmount(form, out var paidAmount) || paidAmount < expectedAmount)
        {
            _logger.LogWarning("YooMoney notification {OperationId} rejected: amount mismatch", operationId);
            return null;
        }

        _verifiedPayments[operationId] = new PaymentVerification
        {
            PaymentId = operationId,
            Status = "succeeded",
            Paid = true,
            UserId = parsedLabel.UserId,
            ProductType = parsedLabel.ProductType,
            TarotPlusSessionId = parsedLabel.TarotPlusSessionId
        };

        return new PaymentWebhookEvent
        {
            Type = PaymentWebhookEventType.PaymentSucceeded,
            PaymentId = operationId,
            UserId = parsedLabel.UserId,
            ProductType = parsedLabel.ProductType,
            TarotPlusSessionId = parsedLabel.TarotPlusSessionId
        };
    }

    public Task<PaymentVerification?> VerifyPaymentAsync(string paymentId, CancellationToken ct = default)
    {
        _verifiedPayments.TryGetValue(paymentId, out var verification);
        return Task.FromResult<PaymentVerification?>(verification);
    }

    private string BuildQuickpayUrl(string label, PaymentCreateRequest request)
    {
        var fields = new Dictionary<string, string>
        {
            ["receiver"] = _options.Receiver,
            ["quickpay-form"] = _options.QuickpayForm,
            ["paymentType"] = _options.PaymentType,
            ["sum"] = request.AmountRub.ToString("F2", CultureInfo.InvariantCulture),
            ["label"] = label,
            ["targets"] = string.IsNullOrWhiteSpace(request.Description) ? _options.Targets : request.Description,
            ["successURL"] = BuildReturnUrl(request.ReturnPath)
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

    private string BuildReturnUrl(string? returnPath)
    {
        if (string.IsNullOrWhiteSpace(returnPath))
            return _options.ReturnUrl;

        if (!Uri.TryCreate(_options.ReturnUrl, UriKind.Absolute, out var baseUri))
            return _options.ReturnUrl;

        var builder = new UriBuilder(baseUri)
        {
            Path = returnPath.StartsWith('/') ? returnPath : "/" + returnPath,
            Query = string.Empty,
            Fragment = string.Empty
        };
        return builder.Uri.ToString();
    }

    private static string CreateLabel(PaymentCreateRequest request)
    {
        var suffix = Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();
        if (request.ProductType == PaymentProductType.TarotPlusSession && request.TarotPlusSessionId is { } sessionId)
            return $"fv:tp:{request.UserId:N}:{sessionId:N}:{suffix}";

        return $"fv:sub:{request.UserId:N}:{suffix}";
    }

    private static ParsedLabel? TryParseLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label)) return null;

        var parts = label.Split(':');
        if (parts.Length < 2 || !string.Equals(parts[0], "fv", StringComparison.Ordinal))
            return null;

        if (parts.Length >= 4
            && string.Equals(parts[1], "sub", StringComparison.Ordinal)
            && Guid.TryParseExact(parts[2], "N", out var subscriptionUserId))
        {
            return new ParsedLabel(PaymentProductType.Subscription, subscriptionUserId, null);
        }

        if (parts.Length >= 5
            && string.Equals(parts[1], "tp", StringComparison.Ordinal)
            && Guid.TryParseExact(parts[2], "N", out var tarotPlusUserId)
            && Guid.TryParseExact(parts[3], "N", out var tarotPlusSessionId))
        {
            return new ParsedLabel(PaymentProductType.TarotPlusSession, tarotPlusUserId, tarotPlusSessionId);
        }

        if (Guid.TryParseExact(parts[1], "N", out var legacyUserId))
            return new ParsedLabel(PaymentProductType.Subscription, legacyUserId, null);

        return null;
    }

    private decimal ExpectedAmount(PaymentProductType productType) =>
        productType == PaymentProductType.TarotPlusSession
            ? _options.TarotPlusPriceAmount
            : _options.MonthlyPriceAmount;

    private void EnsureExpectedAmount(PaymentCreateRequest request)
    {
        var expectedAmount = ExpectedAmount(request.ProductType);
        if (request.AmountRub != expectedAmount)
            throw new InvalidOperationException(
                $"YooMoney {request.ProductType} amount must be {expectedAmount.ToString("F2", CultureInfo.InvariantCulture)} {_options.CurrencyCode}");
    }

    private static bool IsTrue(string? value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    private sealed record ParsedLabel(
        PaymentProductType ProductType,
        Guid UserId,
        Guid? TarotPlusSessionId);
}
