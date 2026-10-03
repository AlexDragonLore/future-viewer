using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Interfaces;
using FutureViewer.Integration.Tests.Fixtures;
using FutureViewer.Infrastructure.Payment;
using FutureViewer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class SubscriptionEndpointTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public SubscriptionEndpointTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Status_without_token_returns_unauthorized()
    {
        var client = _fixture.CreateClient();

        var response = await client.GetAsync("/api/subscription/status");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Subscribe_is_unavailable_until_payment_and_activation_are_both_enabled(
        bool enabled, bool webhookEnabled)
    {
        using var factory = _fixture.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.PostConfigure<PaymentOptions>(options =>
            {
                options.Enabled = enabled;
                options.WebhookEnabled = webhookEnabled;
            })));
        using var client = factory.CreateClient();
        var auth = await _fixture.RegisterAndLoginAsync(client,
            $"payment-switch-{Guid.NewGuid():N}@example.com", "password123");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var response = await client.PostAsJsonAsync("/api/payments/subscribe", new CreatePaymentRequest
        {
            OfferAccepted = true,
            OfferVersion = AuthTestExtensions.LegalDocumentVersion
        });

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync()).Should().Contain("feature_disabled");
    }

    [Fact]
    public async Task Disabled_webhook_does_not_acknowledge_payment_as_processed()
    {
        using var factory = _fixture.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.PostConfigure<PaymentOptions>(options => options.WebhookEnabled = false)));
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/payments/webhook", new { });

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync()).Should().Contain("feature_disabled");
    }

    [Fact]
    public async Task Checkout_records_acceptance_of_the_current_offer_for_the_actual_order()
    {
        var provider = new CheckoutProvider();
        using var factory = _fixture.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.PostConfigure<PaymentOptions>(options =>
            {
                options.Enabled = true;
                options.WebhookEnabled = true;
            });
            services.AddSingleton<IPaymentProvider>(provider);
        }));
        using var client = factory.CreateClient();
        var auth = await _fixture.RegisterAndLoginAsync(client,
            $"checkout-consent-{Guid.NewGuid():N}@example.com", "password123");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var missing = await client.PostAsJsonAsync("/api/payments/subscribe", new CreatePaymentRequest());
        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var stale = await client.PostAsJsonAsync("/api/payments/subscribe", new CreatePaymentRequest
        {
            OfferAccepted = true,
            OfferVersion = "outdated-offer"
        });
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        provider.Calls.Should().Be(0);
        var response = await client.PostAsJsonAsync("/api/payments/subscribe", new CreatePaymentRequest
        {
            OfferAccepted = true,
            OfferVersion = AuthTestExtensions.LegalDocumentVersion
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payment = await response.Content.ReadFromJsonAsync<PaymentCreationDto>();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.PaymentOrders.SingleAsync(x => x.PublicId == Guid.Parse(payment!.PaymentId));
        var consent = await db.UserConsents.SingleAsync(x =>
            x.UserId == auth.UserId && x.CollectionSource == $"checkout:{order.PublicId:N}");
        consent.ConsentType.Should().Be(ConsentType.OfferAcceptance);
        consent.DocumentVersion.Should().Be(AuthTestExtensions.LegalDocumentVersion);
        consent.ContentHash.Should().Be(new string('a', 64));
        consent.AcceptedAt.Should().BeBefore(DateTime.UtcNow.AddSeconds(1));
        provider.Calls.Should().Be(1);

        var renewed = await client.PostAsJsonAsync("/api/payments/subscribe", new CreatePaymentRequest
        {
            OfferAccepted = true,
            OfferVersion = AuthTestExtensions.LegalDocumentVersion
        });
        renewed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await db.UserConsents.CountAsync(x => x.UserId == auth.UserId
            && x.ConsentType == ConsentType.OfferAcceptance && x.RevokedAt == null))
            .Should().Be(3, "registration and each checkout retain their own unrevoked acceptance evidence");
        provider.Calls.Should().Be(2);
    }

    [Fact]
    public async Task Status_for_new_user_reports_free_tier()
    {
        var client = _fixture.CreateClient();
        var email = $"sub-{Guid.NewGuid():N}@example.com";

        var auth = await _fixture.RegisterAndLoginAsync(client, email, "password123");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var response = await client.GetAsync("/api/subscription/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var status = await response.Content.ReadFromJsonAsync<SubscriptionStatusDto>();
        status.Should().NotBeNull();
        status!.Status.Should().Be(SubscriptionStatus.None);
        status.IsActive.Should().BeFalse();
        status.FreeReadingsUsedToday.Should().Be(0);
        status.FreeReadingsDailyLimit.Should().BeGreaterThan(0);
        status.CanCreateFreeReading.Should().BeTrue();
    }

    [Fact]
    public async Task Status_for_active_subscriber_is_active()
    {
        var client = _fixture.CreateClient();
        var email = $"sub-active-{Guid.NewGuid():N}@example.com";

        var auth = await _fixture.RegisterAndLoginAsync(client, email, "password123");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        using (var scope = _fixture.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var user = await users.GetByIdAsync(auth.UserId);
            user!.SubscriptionStatus = SubscriptionStatus.Active;
            user.SubscriptionExpiresAt = DateTime.UtcNow.AddDays(30);
            await users.UpdateAsync(user);
        }

        var response = await client.GetAsync("/api/subscription/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var status = await response.Content.ReadFromJsonAsync<SubscriptionStatusDto>();
        status!.Status.Should().Be(SubscriptionStatus.Active);
        status.IsActive.Should().BeTrue();
        status.CanCreateFreeReading.Should().BeTrue();
    }

    [Fact]
    public async Task Free_user_is_limited_to_single_card_spread()
    {
        var client = _fixture.CreateClient();
        var email = $"sub-limit-{Guid.NewGuid():N}@example.com";

        var auth = await _fixture.RegisterAndLoginAsync(client, email, "password123");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var response = await client.PostAsJsonAsync("/api/readings",
            new CreateReadingRequest { SpreadType = SpreadType.ThreeCard, Question = "test" });

        response.StatusCode.Should().Be(HttpStatusCode.PaymentRequired);
    }

    [Fact]
    public async Task Free_user_exceeding_daily_single_card_quota_gets_429()
    {
        var client = _fixture.CreateClient();
        var email = $"sub-quota-{Guid.NewGuid():N}@example.com";

        var auth = await _fixture.RegisterAndLoginAsync(client, email, "password123");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var first = await client.PostAsJsonAsync("/api/readings",
            new CreateReadingRequest { SpreadType = SpreadType.SingleCard, Question = "first" });
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync("/api/readings",
            new CreateReadingRequest { SpreadType = SpreadType.SingleCard, Question = "second" });
        second.StatusCode.Should().Be((HttpStatusCode)429);
    }

    private sealed class CheckoutProvider : IPaymentProvider
    {
        public int Calls { get; private set; }
        public string ProviderName => "checkout-test";
        public PaymentProductDescriptor Product => new()
        {
            TariffCode = "pro-30d", Amount = 300m, Currency = "RUB", AccessDays = 30
        };

        public Task<PaymentCreationResult> CreateSubscriptionPaymentAsync(
            Guid publicOrderId, string idempotencyKey, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(new PaymentCreationResult
            {
                PaymentId = $"checkout-test-{publicOrderId:N}",
                ConfirmationUrl = "https://provider.example/checkout",
                Status = "pending"
            });
        }

        public PaymentWebhookEvent? ParseWebhook(string body) => null;
        public Task<PaymentVerification?> VerifyPaymentAsync(string paymentId, CancellationToken ct = default) =>
            Task.FromResult<PaymentVerification?>(null);
    }
}
