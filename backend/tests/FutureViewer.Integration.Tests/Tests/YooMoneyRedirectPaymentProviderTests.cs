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
    [Fact]
    public async Task CreateSubscriptionPaymentAsync_returns_YooMoney_redirect_url_with_label()
    {
        var provider = CreateProvider();
        var userId = Guid.Parse("9fa80672-2861-46f3-8e24-120d27a9fd1e");

        var result = await provider.CreateSubscriptionPaymentAsync(userId, "user@example.com");

        result.Status.Should().Be("pending");
        result.PaymentId.Should().StartWith($"fv:{userId:N}:");
        result.ConfirmationUrl.Should().StartWith("https://yoomoney.ru/quickpay/confirm?");
        result.ConfirmationUrl.Should().Contain("receiver=4100111111111111");
        result.ConfirmationUrl.Should().Contain("quickpay-form=button");
        result.ConfirmationUrl.Should().Contain("paymentType=AC");
        result.ConfirmationUrl.Should().Contain("sum=300.00");
        result.ConfirmationUrl.Should().Contain($"label=fv%3A{userId:N}%3A");
        result.ConfirmationUrl.Should().Contain("successURL=http%3A%2F%2Flocalhost%3A5173%2Fpayment%2Fsuccess");
    }

    [Fact]
    public async Task ParseWebhook_accepts_signed_YooMoney_notification_and_marks_payment_verified()
    {
        var provider = CreateProvider();
        var userId = Guid.Parse("6c74a756-ea53-4a16-a881-38be4c392d97");
        var operationId = "441361714955017004";
        var body = CreateSignedWebhookBody(userId, operationId);

        var evt = provider.ParseWebhook(body);
        var verification = await provider.VerifyPaymentAsync(operationId);

        evt.Should().NotBeNull();
        evt!.Type.Should().Be(FutureViewer.DomainServices.Interfaces.PaymentWebhookEventType.PaymentSucceeded);
        evt.PaymentId.Should().Be(operationId);
        evt.UserId.Should().Be(userId);

        verification.Should().NotBeNull();
        verification!.PaymentId.Should().Be(operationId);
        verification.Paid.Should().BeTrue();
        verification.UserId.Should().Be(userId);
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
    public void ParseWebhook_rejects_underpaid_notification()
    {
        var provider = CreateProvider();
        var userId = Guid.Parse("f3f069ad-13f1-4910-90e0-75329312d6e3");
        var body = CreateSignedWebhookBody(userId, "operation-2", withdrawAmount: "299.99", amount: "290.99");

        var evt = provider.ParseWebhook(body);

        evt.Should().BeNull();
    }

    private static YooMoneyRedirectPaymentProvider CreateProvider()
    {
        var options = Options.Create(new YooMoneyOptions
        {
            Receiver = "4100111111111111",
            NotificationSecret = "secret123",
            ReturnUrl = "http://localhost:5173/payment/success",
            MonthlyPriceAmount = 300m,
            Targets = "Future Viewer Pro"
        });

        return new YooMoneyRedirectPaymentProvider(
            options,
            NullLogger<YooMoneyRedirectPaymentProvider>.Instance);
    }

    private static string CreateSignedWebhookBody(
        Guid userId,
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
            ["label"] = $"fv:{userId:N}:abc123",
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
