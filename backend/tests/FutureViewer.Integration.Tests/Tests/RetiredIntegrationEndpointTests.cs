using System.Net;
using FluentAssertions;
using FutureViewer.Integration.Tests.Fixtures;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class RetiredIntegrationEndpointTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;
    public RetiredIntegrationEndpointTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData("GET", "/api/telegram/status")]
    [InlineData("POST", "/api/telegram/link")]
    [InlineData("DELETE", "/api/telegram/link")]
    [InlineData("DELETE", "/api/privacy/integrations/telegram")]
    [InlineData("PUT", "/api/admin/users/11111111-1111-1111-1111-111111111111/telegram")]
    [InlineData("DELETE", "/api/admin/users/11111111-1111-1111-1111-111111111111/telegram")]
    [InlineData("GET", "/api/feedbacks/my")]
    [InlineData("GET", "/api/feedbacks/retired-token")]
    [InlineData("POST", "/api/feedbacks/retired-token")]
    [InlineData("GET", "/api/admin/feedbacks")]
    [InlineData("POST", "/api/admin/feedbacks")]
    [InlineData("POST", "/api/admin/feedbacks/synthetic")]
    [InlineData("PUT", "/api/admin/feedbacks/11111111-1111-1111-1111-111111111111")]
    [InlineData("DELETE", "/api/admin/feedbacks/11111111-1111-1111-1111-111111111111")]
    [InlineData("POST", "/api/admin/feedbacks/run-notifications")]
    [InlineData("GET", "/api/leaderboard/monthly")]
    [InlineData("GET", "/api/leaderboard/alltime")]
    [InlineData("GET", "/api/leaderboard/me")]
    public async Task Removed_integration_routes_are_unavailable(string method, string path)
    {
        using var client = _fixture.CreateClient();
        using var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
