using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Interfaces;
using FutureViewer.Integration.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using FutureViewer.Domain.Enums;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class LeaderboardEndpointTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public LeaderboardEndpointTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Monthly_leaderboard_is_anonymous_and_returns_ok()
    {
        var client = _fixture.CreateClient();
        var response = await client.GetAsync("/api/leaderboard/monthly");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Alltime_leaderboard_is_anonymous_and_returns_ok()
    {
        var client = _fixture.CreateClient();
        var response = await client.GetAsync("/api/leaderboard/alltime");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Leaderboard_me_requires_authentication()
    {
        var client = _fixture.CreateClient();
        var response = await client.GetAsync("/api/leaderboard/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Leaderboard_me_returns_zero_summary_for_new_user()
    {
        var client = _fixture.CreateClient();
        var email = $"lb-{Guid.NewGuid():N}@example.com";

        var register = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Email = email, Password = "password123" });
        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var response = await client.GetAsync("/api/leaderboard/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await response.Content.ReadFromJsonAsync<UserScoreSummaryDto>();
        summary.Should().NotBeNull();
        summary!.TotalScore.Should().Be(0);
        summary.FeedbackCount.Should().Be(0);
    }
}
