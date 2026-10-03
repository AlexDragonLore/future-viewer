using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using FutureViewer.Integration.Tests.Fixtures;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class PublicEndpointTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public PublicEndpointTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Get_config_does_not_invent_an_unverified_support_email()
    {
        var client = _fixture.CreateClient();

        var response = await client.GetAsync("/api/public/config");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<PublicConfigPayload>();
        payload.Should().NotBeNull();
        payload!.SupportEmail.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false, false, true, false)]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, true, false)]
    [InlineData(true, true, true, true)]
    [InlineData(true, true, false, false)]
    public async Task Get_config_only_advertises_payments_when_enabled_and_the_provider_is_configured(
        bool enabled, bool webhookEnabled, bool configured, bool expected)
    {
        await using var factory = _fixture.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Payment:Enabled"] = enabled.ToString(),
                    ["Payment:WebhookEnabled"] = webhookEnabled.ToString(),
                    ["Payment:Provider"] = "Yukassa",
                    ["Yukassa:ShopId"] = configured ? "qa-shop" : "",
                    ["Yukassa:SecretKey"] = configured ? "qa-key" : "",
                })));
        using var client = factory.CreateClient();
        var payload = await client.GetFromJsonAsync<PublicConfigPayload>("/api/public/config");
        payload!.PaymentsEnabled.Should().Be(expected);
    }

    private sealed record PublicConfigPayload(string SupportEmail, bool PaymentsEnabled);
}
