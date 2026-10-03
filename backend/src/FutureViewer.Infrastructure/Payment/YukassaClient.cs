using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FutureViewer.DomainServices.Interfaces;
using FutureViewer.Infrastructure.Compliance;
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

    public YukassaClient(
        HttpClient http,
        IOptions<YukassaOptions> options,
        ILogger<YukassaClient> logger,
        IProcessorRegistryGuard? registry = null)
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

    public string ProviderName => "YooKassa";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ShopId)
        && !string.IsNullOrWhiteSpace(_options.SecretKey)
        && PaymentEndpointSafety.IsYukassaApi(_options.ApiBaseUrl)
        && _options.MonthlyPriceAmount > 0 && decimal.Round(_options.MonthlyPriceAmount, 2) == _options.MonthlyPriceAmount
        && string.Equals(_options.Currency, "RUB", StringComparison.OrdinalIgnoreCase);

    public PaymentProductDescriptor Product => new()
    {
        TariffCode = "pro-30d",
        Amount = _options.MonthlyPriceAmount,
        Currency = _options.Currency,
        AccessDays = 30
    };

    public bool IsWebhookSourceAllowed(string? sourceAddress)
    {
        if (!IPAddress.TryParse(sourceAddress, out var address)) return false;
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        return IsInCidr(address, "185.71.76.0", 27)
               || IsInCidr(address, "185.71.77.0", 27)
               || IsInCidr(address, "77.75.153.0", 25)
               || address.Equals(IPAddress.Parse("77.75.156.11"))
               || address.Equals(IPAddress.Parse("77.75.156.35"))
               || IsInCidr(address, "77.75.154.128", 25)
               || IsInCidr(address, "2a02:5180::", 32);
    }

    public async Task<PaymentCreationResult> CreateSubscriptionPaymentAsync(
        Guid publicOrderId,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ShopId) || string.IsNullOrWhiteSpace(_options.SecretKey))
            throw new InvalidOperationException("Yukassa credentials are not configured");
        PaymentEndpointSafety.EnsureYukassaApi(_options.ApiBaseUrl);

        var request = new CreatePaymentRequest
        {
            Amount = new AmountDto
            {
                Value = _options.MonthlyPriceAmount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                Currency = _options.Currency
            },
            Capture = true,
            Confirmation = new ConfirmationDto
            {
                Type = "redirect",
                ReturnUrl = _options.ReturnUrl
            },
            Description = "Разовый доступ к сервису «Вуаль Грядущего» на 30 дней без автопродления",
            Metadata = new Dictionary<string, string>
            {
                ["access_type"] = "manual_renewal",
                ["order_id"] = publicOrderId.ToString("N")
            }
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, "payments")
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        message.Headers.Add("Idempotence-Key", idempotencyKey);

        using var response = await _http.SendAsync(message, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("YooKassa payment creation failed: status={Status}", response.StatusCode);
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
        PaymentEndpointSafety.EnsureYukassaApi(_options.ApiBaseUrl);

        using var response = await _http.GetAsync($"payments/{Uri.EscapeDataString(paymentId)}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("YooKassa payment verification failed: status={Status}", response.StatusCode);
            return null;
        }

        var payment = await response.Content.ReadFromJsonAsync<PaymentDetailResponse>(JsonOptions, ct);
        if (payment is null) return null;
        if (!string.Equals(payment.Id, paymentId, StringComparison.Ordinal)) return null;

        if (payment.Amount is null
            || !decimal.TryParse(payment.Amount.Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var paidAmount)
            || paidAmount <= 0
            || !string.Equals(payment.Amount.Currency, "RUB", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Yukassa payment {PaymentId} rejected: invalid amount or currency",
                payment.Id);
            return null;
        }

        Guid? orderId = null;
        if (payment.Metadata is not null
            && payment.Metadata.TryGetValue("order_id", out var orderIdString)
            && Guid.TryParseExact(orderIdString, "N", out var parsed))
        {
            orderId = parsed;
        }

        return new PaymentVerification
        {
            PaymentId = payment.Id,
            Status = payment.Status,
            Paid = payment.Paid,
            UserId = null,
            OrderId = orderId,
            Amount = paidAmount,
            Currency = payment.Amount.Currency
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

            Guid? orderId = null;
            if (envelope.Object.Metadata is not null
                && envelope.Object.Metadata.TryGetValue("order_id", out var orderIdString)
                && Guid.TryParseExact(orderIdString, "N", out var parsed))
            {
                orderId = parsed;
            }

            return new PaymentWebhookEvent
            {
                Type = type,
                PaymentId = envelope.Object.Id,
                UserId = null,
                OrderId = orderId
            };
        }
        catch (JsonException)
        {
            _logger.LogWarning("Failed to parse YooKassa webhook body; content was not logged");
            return null;
        }
    }

    private static bool IsInCidr(IPAddress address, string networkText, int prefixLength)
    {
        var network = IPAddress.Parse(networkText);
        var addressBytes = address.GetAddressBytes();
        var networkBytes = network.GetAddressBytes();
        if (addressBytes.Length != networkBytes.Length) return false;

        var fullBytes = prefixLength / 8;
        var remainingBits = prefixLength % 8;
        for (var i = 0; i < fullBytes; i++)
        {
            if (addressBytes[i] != networkBytes[i]) return false;
        }

        if (remainingBits == 0) return true;
        var mask = (byte)(0xff << (8 - remainingBits));
        return (addressBytes[fullBytes] & mask) == (networkBytes[fullBytes] & mask);
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
