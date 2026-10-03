using System.Net;
using FluentAssertions;
using FutureViewer.Infrastructure.Payment;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class YukassaClientTests
{
    [Fact]
    public async Task CreateSubscriptionPaymentAsync_accepts_confirmation_url_without_return_url()
    {
        var handler = new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "id": "pay-prod-1",
                  "status": "pending",
                  "confirmation": {
                    "type": "redirect",
                    "confirmation_url": "https://yoomoney.ru/checkout/payments/v2/contract?orderId=pay-prod-1"
                  }
                }
                """)
        });
        var client = CreateClient(handler);
        var publicOrderId = Guid.Parse("0fb2c969-6de1-4efa-aadb-0d3c5e71df45");

        var result = await client.CreateSubscriptionPaymentAsync(
            publicOrderId,
            "6dc06929b7c64121955396daed93c358");

        result.PaymentId.Should().Be("pay-prod-1");
        result.Status.Should().Be("pending");
        result.ConfirmationUrl.Should().Be("https://yoomoney.ru/checkout/payments/v2/contract?orderId=pay-prod-1");
        handler.RequestBody.Should().Contain($"\"order_id\":\"{publicOrderId:N}\"");
        handler.RequestBody!.Contains("email", StringComparison.OrdinalIgnoreCase).Should().BeFalse();
        handler.IdempotenceKey.Should().Be("6dc06929b7c64121955396daed93c358");
    }

    [Theory]
    [InlineData("185.71.76.10", true)]
    [InlineData("77.75.156.11", true)]
    [InlineData("2a02:5180::42", true)]
    [InlineData("8.8.8.8", false)]
    [InlineData("not-an-ip", false)]
    public void Webhook_source_is_restricted_to_official_ranges(string address, bool expected)
    {
        var client = CreateClient(new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)));

        client.IsWebhookSourceAllowed(address).Should().Be(expected);
    }

    [Fact]
    public async Task VerifyPayment_returns_original_payment_amount_after_current_price_changes()
    {
        var handler = new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {"id":"pay-original","status":"succeeded","paid":true,
                 "amount":{"value":"250.00","currency":"RUB"},
                 "metadata":{"order_id":"6c74a756ea534a16a88138be4c392d97"}}
                """)
        });

        var verified = await CreateClient(handler).VerifyPaymentAsync("pay-original");

        verified.Should().NotBeNull();
        verified!.Amount.Should().Be(250m);
        verified.OrderId.Should().Be(Guid.Parse("6c74a756-ea53-4a16-a881-38be4c392d97"));
    }

    [Theory]
    [InlineData("pay-other", "300.00", "RUB")]
    [InlineData("pay-original", "0.00", "RUB")]
    [InlineData("pay-original", "-300.00", "RUB")]
    [InlineData("pay-original", "3,00", "RUB")]
    [InlineData("pay-original", "300.00", "USD")]
    public async Task VerifyPayment_rejects_wrong_identity_or_invalid_amount_currency(
        string id, string amount, string currency)
    {
        var handler = new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(new
            {
                id,
                status = "succeeded",
                paid = true,
                amount = new { value = amount, currency }
            }))
        });

        (await CreateClient(handler).VerifyPaymentAsync("pay-original")).Should().BeNull();
    }

    private static YukassaClient CreateClient(HttpMessageHandler handler)
    {
        var options = Options.Create(new YukassaOptions
        {
            ShopId = "516089",
            SecretKey = "test-secret",
            ReturnUrl = "https://alex-taro.ru/payment/success",
            MonthlyPriceAmount = 300m
        });

        return new YukassaClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.yookassa.ru/v3/") },
            options,
            NullLogger<YukassaClient>.Instance);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;
        public string? RequestBody { get; private set; }
        public string? IdempotenceKey { get; private set; }

        public StubHttpMessageHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            IdempotenceKey = request.Headers.TryGetValues("Idempotence-Key", out var keys)
                ? keys.SingleOrDefault()
                : null;
            return _response;
        }
    }
}
