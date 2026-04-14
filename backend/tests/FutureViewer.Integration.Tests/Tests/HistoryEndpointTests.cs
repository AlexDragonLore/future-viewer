using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.Integration.Tests.Fixtures;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class HistoryEndpointTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public HistoryEndpointTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task History_returns_user_readings()
    {
        var client = _fixture.CreateClient();
        var email = $"hist-{Guid.NewGuid():N}@example.com";

        var register = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Email = email, Password = "password123" });
        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        await client.PostAsJsonAsync("/api/readings",
            new CreateReadingRequest { SpreadType = SpreadType.SingleCard, Question = "q1" });
        await client.PostAsJsonAsync("/api/readings",
            new CreateReadingRequest { SpreadType = SpreadType.ThreeCard, Question = "q2" });

        var response = await client.GetAsync("/api/readings/history");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var history = await response.Content.ReadFromJsonAsync<List<ReadingResult>>();
        history.Should().NotBeNull();
        history!.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task History_without_token_returns_unauthorized()
    {
        var client = _fixture.CreateClient();
        var response = await client.GetAsync("/api/readings/history");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
