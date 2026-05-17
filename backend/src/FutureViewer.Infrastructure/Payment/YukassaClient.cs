using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FutureViewer.Infrastructure.Payment;

public sealed class YukassaClient : IPaymentProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly YukassaOptions _options;
    private readonly ILogger<YukassaClient> _logger;

    public YukassaClient(HttpClient http, IOptions<YukassaOptions> options, ILogger<YukassaClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;

        if (_http.BaseAddress is null)
            _http.BaseAddress = new Uri(_options.ApiBaseUrl);

        if (!string.IsNullOrWhiteSpace(_options.ShopId) && !string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ShopId}:{_options.SecretKey}"));
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
        }
    }

    public async Task<PaymentCreationResult> CreateSubscriptionPaymentAsync(
        Guid userId,
        string userEmail,
        CancellationToken ct = default)
    {
        return await CreatePaymentAsync(new PaymentCreateRequest
        {
            UserId = userId,
            UserEmail = userEmail,
            ProductType = PaymentProductType.Subscription,
            AmountRub = _options.MonthlyPriceAmount,
            Description = $"Разовое продление доступа «Вуаль Грядущего» для {userEmail}"
        }, ct);
    }

    public async Task<PaymentCreationResult> CreatePaymentAsync(
        PaymentCreateRequest paymentRequest,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ShopId) || string.IsNullOrWhiteSpace(_options.SecretKey))
            throw new InvalidOperationException("Yukassa credentials are not configured");
        EnsureExpectedAmount(paymentRequest);

        var metadata = new Dictionary<string, string>
        {
            ["product_type"] = ToMetadataProductType(paymentRequest.ProductType),
            ["user_id"] = paymentRequest.UserId.ToString()
        };
        if (paymentRequest.TarotPlusSessionId is { } sessionId)
            metadata["tarot_plus_session_id"] = sessionId.ToString();

        var request = new CreatePaymentRequest
        {
            Amount = new AmountDto
            {
                Value = paymentRequest.AmountRub.ToString("F2", CultureInfo.InvariantCulture),
                Currency = _options.Currency
            },
            Capture = true,
            Confirmation = new ConfirmationDto
            {
                Type = "redirect",
                ReturnUrl = BuildReturnUrl(paymentRequest.ReturnPath)
            },
            Description = paymentRequest.Description,
            Metadata = metadata
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, "payments")
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        message.Headers.Add("Idempotence-Key", Guid.NewGuid().ToString());

        using var response = await _http.SendAsync(message, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Yukassa payment creation failed: {Status} {Body}", response.StatusCode, errorBody);
            throw new InvalidOperationException($"Yukassa payment creation failed: {response.StatusCode}");
        }

        var payment = await response.Content.ReadFromJsonAsync<PaymentResponse>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Yukassa returned empty payment body");

        if (string.IsNullOrWhiteSpace(payment.Confirmation?.ConfirmationUrl))
            _logger.LogWarning("Yukassa payment {PaymentId} returned without confirmation_url", payment.Id);

        return new PaymentCreationResult
        {
            PaymentId = payment.Id,
            ConfirmationUrl = payment.Confirmation?.ConfirmationUrl ?? string.Empty,
            Status = payment.Status
        };
    }

    public async Task<PaymentVerification?> VerifyPaymentAsync(string paymentId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(paymentId)) return null;
        if (string.IsNullOrWhiteSpace(_options.ShopId) || string.IsNullOrWhiteSpace(_options.SecretKey))
            throw new InvalidOperationException("Yukassa credentials are not configured");

        using var response = await _http.GetAsync($"payments/{Uri.EscapeDataString(paymentId)}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Yukassa payment verification failed: {Status} {Body}", response.StatusCode, errorBody);
            return null;
        }

        var payment = await response.Content.ReadFromJsonAsync<PaymentDetailResponse>(JsonOptions, ct);
        if (payment is null) return null;

        var productType = ReadProductType(payment.Metadata);
        var expectedAmount = ExpectedAmount(productType);

        if (payment.Amount is null
            || !decimal.TryParse(payment.Amount.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var paidAmount)
            || paidAmount < expectedAmount
            || !string.Equals(payment.Amount.Currency, _options.Currency, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Yukassa payment {PaymentId} rejected: amount/currency mismatch (got {Amount} {Currency}, expected {ExpectedAmount} {ExpectedCurrency})",
                payment.Id, payment.Amount?.Value, payment.Amount?.Currency, expectedAmount, _options.Currency);
            return null;
        }

        Guid? userId = null;
        Guid? tarotPlusSessionId = null;
        if (payment.Metadata is not null
            && payment.Metadata.TryGetValue("user_id", out var userIdStr)
            && Guid.TryParse(userIdStr, out var parsed))
        {
            userId = parsed;
        }
        if (payment.Metadata is not null
            && payment.Metadata.TryGetValue("tarot_plus_session_id", out var sessionIdStr)
            && Guid.TryParse(sessionIdStr, out var sessionId))
        {
            tarotPlusSessionId = sessionId;
        }

        return new PaymentVerification
        {
            PaymentId = payment.Id,
            Status = payment.Status,
            Paid = payment.Paid,
            UserId = userId,
            ProductType = productType,
            TarotPlusSessionId = tarotPlusSessionId
        };
    }

    public PaymentWebhookEvent? ParseWebhook(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;

        try
        {
            var envelope = JsonSerializer.Deserialize<WebhookEnvelope>(body, JsonOptions);
            if (envelope is null || envelope.Object is null) return null;

            var type = envelope.Event switch
            {
                "payment.succeeded" => PaymentWebhookEventType.PaymentSucceeded,
                "payment.canceled" => PaymentWebhookEventType.PaymentCanceled,
                _ => PaymentWebhookEventType.Unknown
            };

            Guid? userId = null;
            Guid? tarotPlusSessionId = null;
            var productType = ReadProductType(envelope.Object.Metadata);
            if (envelope.Object.Metadata is not null
                && envelope.Object.Metadata.TryGetValue("user_id", out var userIdStr)
                && Guid.TryParse(userIdStr, out var parsed))
            {
                userId = parsed;
            }
            if (envelope.Object.Metadata is not null
                && envelope.Object.Metadata.TryGetValue("tarot_plus_session_id", out var sessionIdStr)
                && Guid.TryParse(sessionIdStr, out var sessionId))
            {
                tarotPlusSessionId = sessionId;
            }

            return new PaymentWebhookEvent
            {
                Type = type,
                PaymentId = envelope.Object.Id,
                UserId = userId,
                ProductType = productType,
                TarotPlusSessionId = tarotPlusSessionId
            };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse Yukassa webhook body");
            return null;
        }
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
                $"Yukassa {request.ProductType} amount must be {expectedAmount.ToString("F2", CultureInfo.InvariantCulture)} {_options.Currency}");
    }

    private static string ToMetadataProductType(PaymentProductType productType) =>
        productType == PaymentProductType.TarotPlusSession ? "tarot_plus_session" : "subscription";

    private static PaymentProductType ReadProductType(IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is not null
            && metadata.TryGetValue("product_type", out var value)
            && string.Equals(value, "tarot_plus_session", StringComparison.OrdinalIgnoreCase))
        {
            return PaymentProductType.TarotPlusSession;
        }

        return PaymentProductType.Subscription;
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

    private sealed class CreatePaymentRequest
    {
        public required AmountDto Amount { get; init; }
        public required bool Capture { get; init; }
        public required ConfirmationDto Confirmation { get; init; }
        public required string Description { get; init; }
        public IReadOnlyDictionary<string, string>? Metadata { get; init; }
    }

    private sealed class AmountDto
    {
        public required string Value { get; init; }
        public required string Currency { get; init; }
    }

    private sealed class ConfirmationDto
    {
        public required string Type { get; init; }
        [JsonPropertyName("return_url")]
        public required string ReturnUrl { get; init; }
        [JsonPropertyName("confirmation_url")]
        public string? ConfirmationUrl { get; init; }
    }

    private sealed class PaymentResponse
    {
        public required string Id { get; init; }
        public required string Status { get; init; }
        public ConfirmationResponseDto? Confirmation { get; init; }
    }

    private sealed class ConfirmationResponseDto
    {
        public string? Type { get; init; }
        [JsonPropertyName("confirmation_url")]
        public string? ConfirmationUrl { get; init; }
    }

    private sealed class WebhookEnvelope
    {
        public string? Event { get; init; }
        public WebhookObject? Object { get; init; }
    }

    private sealed class WebhookObject
    {
        public required string Id { get; init; }
        public string? Status { get; init; }
        public Dictionary<string, string>? Metadata { get; init; }
    }

    private sealed class PaymentDetailResponse
    {
        public required string Id { get; init; }
        public required string Status { get; init; }
        public bool Paid { get; init; }
        public AmountDto? Amount { get; init; }
        public Dictionary<string, string>? Metadata { get; init; }
    }
}
