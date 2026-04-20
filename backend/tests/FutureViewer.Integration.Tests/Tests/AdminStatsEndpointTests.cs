using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.DTOs.Admin;
using FutureViewer.DomainServices.Interfaces;
using FutureViewer.Integration.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class AdminStatsEndpointTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public AdminStatsEndpointTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Stats_endpoint_requires_admin_role()
    {
        var (client, _) = await CreateAuthenticatedClient(asAdmin: false);

        var response = await client.GetAsync("/api/admin/stats");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Stats_endpoint_returns_aggregate_counts()
    {
        var (adminClient, _) = await CreateAuthenticatedClient(asAdmin: true);
        var (userClient, _) = await CreateAuthenticatedSubscribedClient();
        await CreateReading(userClient);

        var response = await adminClient.GetAsync("/api/admin/stats");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stats = await response.Content.ReadFromJsonAsync<AdminStatsDto>();
        stats.Should().NotBeNull();
        stats!.TotalUsers.Should().BeGreaterThanOrEqualTo(2);
        stats.AdminCount.Should().BeGreaterThanOrEqualTo(1);
        stats.ActiveSubscriptions.Should().BeGreaterThanOrEqualTo(1);
        stats.ReadingsToday.Should().BeGreaterThanOrEqualTo(1);
        stats.ReadingsThisWeek.Should().BeGreaterThanOrEqualTo(1);
        stats.PendingFeedbacksToNotify.Should().BeGreaterThanOrEqualTo(0);
        stats.ScoredFeedbacksThisMonth.Should().BeGreaterThanOrEqualTo(0);
    }

    private async Task<Guid> CreateReading(HttpClient client)
    {
        var created = await client.PostAsJsonAsync("/api/readings",
            new CreateReadingRequest { SpreadType = SpreadType.SingleCard, Question = "Today?" });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var reading = await created.Content.ReadFromJsonAsync<ReadingResult>();
        return reading!.Id;
    }

    private async Task<(HttpClient Client, AuthResponse Auth)> CreateAuthenticatedClient(bool asAdmin)
    {
        var client = _fixture.CreateClient();
        var email = $"admin-stats-{Guid.NewGuid():N}@example.com";

        var register = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Email = email, Password = "password123" });
        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>();

        if (asAdmin)
        {
            using var scope = _fixture.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var user = await users.GetByIdAsync(auth!.UserId);
            user!.IsAdmin = true;
            await users.UpdateAsync(user);

            var login = await client.PostAsJsonAsync("/api/auth/login",
                new LoginRequest { Email = email, Password = "password123" });
            auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return (client, auth);
    }

    private async Task<(HttpClient Client, AuthResponse Auth)> CreateAuthenticatedSubscribedClient()
    {
        var client = _fixture.CreateClient();
        var email = $"user-stats-{Guid.NewGuid():N}@example.com";

        var register = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Email = email, Password = "password123" });
        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        using var scope = _fixture.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var user = await users.GetByIdAsync(auth.UserId);
        user!.SubscriptionStatus = SubscriptionStatus.Active;
        user.SubscriptionExpiresAt = DateTime.UtcNow.AddDays(30);
        await users.UpdateAsync(user);

        return (client, auth);
    }
}
