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

        var result = await client.CreateSubscriptionPaymentAsync(
            Guid.Parse("0fb2c969-6de1-4efa-aadb-0d3c5e71df45"),
            "user@example.com");

        result.PaymentId.Should().Be("pay-prod-1");
        result.Status.Should().Be("pending");
        result.ConfirmationUrl.Should().Be("https://yoomoney.ru/checkout/payments/v2/contract?orderId=pay-prod-1");
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

        public StubHttpMessageHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_response);
        }
    }
}
