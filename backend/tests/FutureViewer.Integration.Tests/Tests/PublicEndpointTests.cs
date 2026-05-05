using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using FutureViewer.Integration.Tests.Fixtures;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class PublicEndpointTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public PublicEndpointTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Get_config_returns_support_email_without_auth()
    {
        var client = _fixture.CreateClient();

        var response = await client.GetAsync("/api/public/config");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<PublicConfigPayload>();
        payload.Should().NotBeNull();
        payload!.SupportEmail.Should().NotBeNullOrWhiteSpace();
    }

    private sealed record PublicConfigPayload(string SupportEmail);
}
