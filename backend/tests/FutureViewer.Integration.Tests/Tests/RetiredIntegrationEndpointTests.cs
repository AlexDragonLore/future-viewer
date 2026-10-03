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
    [InlineData("POST", "/api/admin/feedbacks/run-notifications")]
    public async Task Removed_integration_routes_are_unavailable(string method, string path)
    {
        using var client = _fixture.CreateClient();
        using var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));
        // The existing feedback token GET route matches the retired notification path
        // and produces 405; neither status exposes a working integration handler.
        response.StatusCode.Should().Be(path.EndsWith("run-notifications")
            ? HttpStatusCode.MethodNotAllowed : HttpStatusCode.NotFound);
    }
}
