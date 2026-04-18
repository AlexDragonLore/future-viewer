using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Interfaces;
using FutureViewer.Integration.Tests.Fixtures;
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

    [Fact]
    public async Task Status_for_new_user_reports_free_tier()
    {
        var client = _fixture.CreateClient();
        var email = $"sub-{Guid.NewGuid():N}@example.com";

        var register = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Email = email, Password = "password123" });
        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>();
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

        var register = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Email = email, Password = "password123" });
        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>();
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

        var register = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Email = email, Password = "password123" });
        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>();
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

        var register = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Email = email, Password = "password123" });
        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var first = await client.PostAsJsonAsync("/api/readings",
            new CreateReadingRequest { SpreadType = SpreadType.SingleCard, Question = "first" });
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync("/api/readings",
            new CreateReadingRequest { SpreadType = SpreadType.SingleCard, Question = "second" });
        second.StatusCode.Should().Be((HttpStatusCode)429);
    }
}
