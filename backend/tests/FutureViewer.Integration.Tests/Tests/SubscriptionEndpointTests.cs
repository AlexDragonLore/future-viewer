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
        order.TariffCode.Should().Be("pro-30d", "clients omitting the tariff retain the monthly checkout");
        order.Amount.Should().Be(299m);
        order.AccessDays.Should().Be(30);
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
        status.CanCreateIntroReading.Should().BeTrue();
    }

    [Theory]
    [InlineData("pro-7d", 99, 7)]
    [InlineData("pro-30d", 299, 30)]
    public async Task Checkout_persists_and_charges_the_selected_server_tariff(
        string tariffCode, decimal amount, int accessDays)
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
            $"checkout-tariff-{Guid.NewGuid():N}@example.com", "password123");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var response = await client.PostAsJsonAsync("/api/payments/subscribe", new CreatePaymentRequest
        {
            TariffCode = tariffCode,
            OfferAccepted = true,
            OfferVersion = AuthTestExtensions.LegalDocumentVersion
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payment = await response.Content.ReadFromJsonAsync<PaymentCreationDto>();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.PaymentOrders.SingleAsync(x => x.PublicId == Guid.Parse(payment!.PaymentId));
        order.TariffCode.Should().Be(tariffCode);
        order.Amount.Should().Be(amount);
        order.Currency.Should().Be("RUB");
        order.AccessDays.Should().Be(accessDays);
        provider.LastProduct.Should().BeEquivalentTo(new PaymentProductDescriptor
        {
            TariffCode = tariffCode, Amount = amount, Currency = "RUB", AccessDays = accessDays
        });
    }

    [Fact]
    public async Task Checkout_rejects_unknown_tariff_without_creating_a_payment()
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
            $"checkout-unknown-{Guid.NewGuid():N}@example.com", "password123");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var response = await client.PostAsJsonAsync("/api/payments/subscribe", new CreatePaymentRequest
        {
            TariffCode = "pro-1d", OfferAccepted = true, OfferVersion = AuthTestExtensions.LegalDocumentVersion
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        provider.Calls.Should().Be(0);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.PaymentOrders.CountAsync(x => x.UserId == auth.UserId)).Should().Be(0);
        (await db.UserConsents.CountAsync(x => x.UserId == auth.UserId
            && x.CollectionSource.StartsWith("checkout:"))).Should().Be(0);
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
        status.CanCreateIntroReading.Should().BeFalse();
    }

    [Fact]
    public async Task Free_user_gets_one_intro_three_card_reading_then_only_daily_single_card()
    {
        var client = _fixture.CreateClient();
        var email = $"sub-limit-{Guid.NewGuid():N}@example.com";

        var auth = await _fixture.RegisterAndLoginAsync(client, email, "password123");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var response = await client.PostAsJsonAsync("/api/readings",
            new CreateReadingRequest { SpreadType = SpreadType.ThreeCard, Question = "test" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var reading = (await response.Content.ReadFromJsonAsync<ReadingResult>())!;
        reading.Cards.Should().HaveCount(3);
        var status = (await (await client.GetAsync("/api/subscription/status")).Content.ReadFromJsonAsync<SubscriptionStatusDto>())!;
        status.CanCreateIntroReading.Should().BeFalse();
        status.CanCreateFreeReading.Should().BeFalse();
        (await client.PostAsJsonAsync("/api/readings",
            new CreateReadingRequest { SpreadType = SpreadType.ThreeCard, Question = "again" }))
            .StatusCode.Should().Be(HttpStatusCode.PaymentRequired);
        (await client.PostAsJsonAsync("/api/readings",
            new CreateReadingRequest { SpreadType = SpreadType.SingleCard, Question = "today" }))
            .StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        // Move the original usage to yesterday to exercise the existing UTC-day boundary.
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var yesterday = DateTime.UtcNow.Date.AddDays(-1);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE readings SET created_at = {yesterday} WHERE id = {reading.Id}");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE users SET last_reading_at = {yesterday} WHERE id = {auth.UserId}");
        }
        var nextDayStatus = (await (await client.GetAsync("/api/subscription/status")).Content.ReadFromJsonAsync<SubscriptionStatusDto>())!;
        nextDayStatus.CanCreateIntroReading.Should().BeFalse();
        nextDayStatus.CanCreateFreeReading.Should().BeTrue();
        (await client.PostAsJsonAsync("/api/readings",
            new CreateReadingRequest { SpreadType = SpreadType.ThreeCard, Question = "three again" }))
            .StatusCode.Should().Be(HttpStatusCode.PaymentRequired);
        (await client.PostAsJsonAsync("/api/readings",
            new CreateReadingRequest { SpreadType = SpreadType.SingleCard, Question = "next day" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task New_free_user_cannot_create_celtic_cross()
    {
        var client = _fixture.CreateClient();
        var auth = await _fixture.RegisterAndLoginAsync(client, $"sub-celtic-{Guid.NewGuid():N}@example.com", "password123");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        (await client.PostAsJsonAsync("/api/readings",
            new CreateReadingRequest { SpreadType = SpreadType.CelticCross, Question = "test" }))
            .StatusCode.Should().Be(HttpStatusCode.PaymentRequired);
    }

    [Fact]
    public async Task Removing_intro_from_history_does_not_restore_intro_or_daily_quota()
    {
        var client = _fixture.CreateClient();
        var auth = await _fixture.RegisterAndLoginAsync(client, $"sub-delete-{Guid.NewGuid():N}@example.com", "password123");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var response = await client.PostAsJsonAsync("/api/readings",
            new CreateReadingRequest { SpreadType = SpreadType.ThreeCard, Question = "test" });
        response.EnsureSuccessStatusCode();
        var reading = (await response.Content.ReadFromJsonAsync<ReadingResult>())!;

        (await client.DeleteAsync($"/api/readings/{reading.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await (await client.GetAsync("/api/readings/history")).Content.ReadFromJsonAsync<ReadingResult[]>()).Should().BeEmpty();
        var status = (await (await client.GetAsync("/api/subscription/status")).Content.ReadFromJsonAsync<SubscriptionStatusDto>())!;
        status.CanCreateIntroReading.Should().BeFalse();
        status.CanCreateFreeReading.Should().BeFalse();
        (await client.PostAsJsonAsync("/api/readings",
            new CreateReadingRequest { SpreadType = SpreadType.ThreeCard, Question = "again" }))
            .StatusCode.Should().Be(HttpStatusCode.PaymentRequired);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Erasing_reading_content_does_not_restore_intro_or_daily_quota(bool eraseAll)
    {
        var client = _fixture.CreateClient();
        var auth = await _fixture.RegisterAndLoginAsync(client, $"sub-erase-{Guid.NewGuid():N}@example.com", "password123");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var response = await client.PostAsJsonAsync("/api/readings",
            new CreateReadingRequest { SpreadType = SpreadType.ThreeCard, Question = "test" });
        response.EnsureSuccessStatusCode();
        var reading = (await response.Content.ReadFromJsonAsync<ReadingResult>())!;
        var path = eraseAll ? "/api/privacy/readings" : $"/api/privacy/readings/{reading.Id}";
        var delete = new HttpRequestMessage(HttpMethod.Delete, path)
        {
            Content = JsonContent.Create(new ReauthenticationRequest { Password = "password123" })
        };
        (await client.SendAsync(delete)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Readings.AnyAsync(r => r.UserId == auth.UserId)).Should().BeFalse();
        var status = (await (await client.GetAsync("/api/subscription/status")).Content.ReadFromJsonAsync<SubscriptionStatusDto>())!;
        status.CanCreateIntroReading.Should().BeFalse();
        status.CanCreateFreeReading.Should().BeFalse();
        status.FreeReadingsUsedToday.Should().Be(1);
        var exportResponse = await client.PostAsJsonAsync("/api/privacy/export",
            new ReauthenticationRequest { Password = "password123" });
        exportResponse.EnsureSuccessStatusCode();
        var export = (await exportResponse.Content.ReadFromJsonAsync<PrivacyExportDto>())!;
        export.Profile.HasUsedIntroReading.Should().BeTrue();
        export.Profile.LastReadingAt.Should().BeCloseTo(reading.CreatedAt, TimeSpan.FromMicroseconds(1));
        export.Readings.Should().BeEmpty();
        (await client.PostAsJsonAsync("/api/readings",
            new CreateReadingRequest { SpreadType = SpreadType.ThreeCard, Question = "again" }))
            .StatusCode.Should().Be(HttpStatusCode.PaymentRequired);
        (await client.PostAsJsonAsync("/api/readings",
            new CreateReadingRequest { SpreadType = SpreadType.SingleCard, Question = "again" }))
            .StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Concurrent_intro_requests_create_only_one_reading()
    {
        var client = _fixture.CreateClient();
        var auth = await _fixture.RegisterAndLoginAsync(client, $"sub-concurrent-{Guid.NewGuid():N}@example.com", "password123");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var request = new CreateReadingRequest { SpreadType = SpreadType.ThreeCard, Question = "test" };

        var responses = await Task.WhenAll(
            client.PostAsJsonAsync("/api/readings", request),
            client.PostAsJsonAsync("/api/readings", request));

        responses.Select(r => r.StatusCode).Should().BeEquivalentTo([HttpStatusCode.Created, HttpStatusCode.PaymentRequired]);
        var history = await (await client.GetAsync("/api/readings/history")).Content.ReadFromJsonAsync<ReadingResult[]>();
        history.Should().ContainSingle();
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
        public PaymentProductDescriptor? LastProduct { get; private set; }
        public string ProviderName => "checkout-test";
        public PaymentProductDescriptor Product => new()
        {
            TariffCode = "pro-30d", Amount = 299m, Currency = "RUB", AccessDays = 30
        };
        public IReadOnlyList<PaymentProductDescriptor> Products =>
        [
            new() { TariffCode = "pro-7d", Amount = 99m, Currency = "RUB", AccessDays = 7 },
            Product
        ];

        public Task<PaymentCreationResult> CreateSubscriptionPaymentAsync(
            Guid publicOrderId, string idempotencyKey, PaymentProductDescriptor product, CancellationToken ct = default)
        {
            Calls++;
            LastProduct = product;
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
