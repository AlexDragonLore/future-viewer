using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using FutureViewer.Infrastructure.Payment;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class YooMoneyRedirectPaymentProviderTests
{
    [Theory]
    [InlineData("pro-7d", "99.00", 7)]
    [InlineData("pro-30d", "299.00", 30)]
    public async Task CreateSubscriptionPaymentAsync_returns_selected_tariff_amount_with_order_label(
        string tariffCode, string amount, int accessDays)
    {
        var provider = CreateProvider();
        var orderId = Guid.Parse("9fa80672-2861-46f3-8e24-120d27a9fd1e");

        var product = provider.Products.Single(product => product.TariffCode == tariffCode);
        product.AccessDays.Should().Be(accessDays);
        var result = await provider.CreateSubscriptionPaymentAsync(orderId, "idempotency-key", product);

        result.Status.Should().Be("pending");
        result.PaymentId.Should().BeNull("the operation ID is assigned when money is transferred");
        result.ConfirmationUrl.Should().StartWith("https://yoomoney.ru/quickpay/confirm?");
        result.ConfirmationUrl.Should().Contain("receiver=4100111111111111");
        result.ConfirmationUrl.Should().Contain("quickpay-form=button");
        result.ConfirmationUrl.Should().Contain("paymentType=AC");
        result.ConfirmationUrl.Should().Contain($"sum={amount}");
        result.ConfirmationUrl.Should().Contain($"label=fv-order%3A{orderId:N}%3A");
        result.ConfirmationUrl.Should().NotContain("%40");
        result.ConfirmationUrl.Should().Contain("successURL=http%3A%2F%2Flocalhost%3A5173%2Fpayment%2Fsuccess");
    }

    [Fact]
    public async Task ParseWebhook_accepts_signed_YooMoney_notification_and_marks_payment_verified()
    {
        var provider = CreateProvider();
        var orderId = Guid.Parse("6c74a756-ea53-4a16-a881-38be4c392d97");
        var operationId = "441361714955017004";
        var body = CreateSignedWebhookBody(orderId, operationId);

        var evt = provider.ParseWebhook(body);
        var verification = await provider.VerifyPaymentAsync(operationId);

        evt.Should().NotBeNull();
        evt!.Type.Should().Be(FutureViewer.DomainServices.Interfaces.PaymentWebhookEventType.PaymentSucceeded);
        evt.PaymentId.Should().Be(operationId);
        evt.UserId.Should().BeNull();
        evt.OrderId.Should().Be(orderId);

        verification.Should().NotBeNull();
        verification!.PaymentId.Should().Be(operationId);
        verification.Paid.Should().BeTrue();
        verification.UserId.Should().BeNull();
        verification.OrderId.Should().Be(orderId);
        verification.Amount.Should().Be(300m);
        verification.Currency.Should().Be("RUB");
    }

    [Fact]
    public void ParseWebhook_rejects_notification_with_invalid_signature()
    {
        var provider = CreateProvider();
        var userId = Guid.Parse("e1d0500f-c07b-4310-b92a-f29b81634fee");
        var body = CreateSignedWebhookBody(userId, "operation-1") + "0";

        var evt = provider.ParseWebhook(body);

        evt.Should().BeNull();
    }

    [Fact]
    public async Task ParseWebhook_exposes_authenticated_amount_for_comparison_with_original_order_price()
    {
        var provider = CreateProvider();
        var userId = Guid.Parse("f3f069ad-13f1-4910-90e0-75329312d6e3");
        var body = CreateSignedWebhookBody(userId, "operation-2", withdrawAmount: "299.99", amount: "290.99");

        var evt = provider.ParseWebhook(body);

        evt.Should().NotBeNull();
        var verified = await provider.VerifyPaymentAsync("operation-2");
        verified!.Amount.Should().Be(299.99m);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-300")]
    [InlineData("3,00")]
    [InlineData("not-a-number")]
    public void ParseWebhook_rejects_invalid_amounts(string amount)
    {
        var provider = CreateProvider();

        provider.ParseWebhook(CreateSignedWebhookBody(Guid.NewGuid(), "invalid-amount", withdrawAmount: amount))
            .Should().BeNull();
    }

    [Fact]
    public void ParseWebhook_rejects_duplicate_parameters_even_if_last_value_has_valid_signature()
    {
        var provider = CreateProvider();
        var body = "withdraw_amount=1&" + CreateSignedWebhookBody(Guid.NewGuid(), "duplicate-amount");

        provider.ParseWebhook(body).Should().BeNull();
    }

    [Fact]
    public async Task Verified_notification_is_consumed_instead_of_retained_indefinitely()
    {
        var provider = CreateProvider();
        var body = CreateSignedWebhookBody(Guid.NewGuid(), "operation-consumed");

        provider.ParseWebhook(body).Should().NotBeNull();
        (await provider.VerifyPaymentAsync("operation-consumed")).Should().NotBeNull();
        (await provider.VerifyPaymentAsync("operation-consumed")).Should().BeNull();

        provider.ParseWebhook(body).Should().NotBeNull();
        (await provider.VerifyPaymentAsync("operation-consumed")).Should().NotBeNull();
    }

    private static YooMoneyRedirectPaymentProvider CreateProvider()
    {
        var options = Options.Create(new YooMoneyOptions
        {
            Receiver = "4100111111111111",
            NotificationSecret = "secret123",
            ReturnUrl = "http://localhost:5173/payment/success",
            Targets = "Future Viewer Pro"
        });

        return new YooMoneyRedirectPaymentProvider(
            options,
            NullLogger<YooMoneyRedirectPaymentProvider>.Instance);
    }

    private static string CreateSignedWebhookBody(
        Guid orderId,
        string operationId,
        string withdrawAmount = "300.00",
        string amount = "291.00")
    {
        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["notification_type"] = "card-incoming",
            ["operation_id"] = operationId,
            ["amount"] = amount,
            ["withdraw_amount"] = withdrawAmount,
            ["currency"] = "643",
            ["datetime"] = "2026-05-12T20:00:00Z",
            ["sender"] = "",
            ["codepro"] = "false",
            ["label"] = $"fv-order:{orderId:N}:abc123",
            ["unaccepted"] = "false"
        };

        fields["sign"] = Sign(fields, "secret123");

        return string.Join("&", fields.Select(x =>
            $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value)}"));
    }

    private static string Sign(
        IEnumerable<KeyValuePair<string, string>> fields,
        string secret)
    {
        var canonical = string.Join("&", fields
            .Where(x => !string.Equals(x.Key, "sign", StringComparison.Ordinal))
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => $"{x.Key}={Uri.EscapeDataString(x.Value)}"));

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical)))
            .ToLower(CultureInfo.InvariantCulture);
    }
}
