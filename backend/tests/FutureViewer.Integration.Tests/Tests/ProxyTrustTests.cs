using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.Integration.Tests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class ProxyTrustTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public ProxyTrustTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData(false, "192.0.2.10", false, HttpStatusCode.TooManyRequests)]
    [InlineData(true, "192.0.2.20", false, HttpStatusCode.TooManyRequests)]
    [InlineData(true, "192.0.2.10", false, HttpStatusCode.Unauthorized)]
    [InlineData(true, "192.0.2.10", true, HttpStatusCode.TooManyRequests)]
    public async Task Forwarded_headers_only_affect_limits_from_explicitly_trusted_peers(
        bool configured,
        string peer,
        bool spoofedPrefix,
        HttpStatusCode expected)
    {
        var settings = new Dictionary<string, string?> { ["ReverseProxy:ForwardLimit"] = "1" };
        if (configured)
            settings["ReverseProxy:KnownProxies:0"] = "192.0.2.10";
        using var factory = _fixture.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(settings)));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Remove("X-Test-Client-IP");
        client.DefaultRequestHeaders.Add("X-Test-Client-IP", peer);
        HttpStatusCode actual = default;
        for (var attempt = 0; attempt < 11; attempt++)
        {
            client.DefaultRequestHeaders.Remove("X-Forwarded-For");
            client.DefaultRequestHeaders.Add("X-Forwarded-For", spoofedPrefix
                ? $"198.51.100.{attempt + 1}, 203.0.113.20"
                : $"198.51.100.{attempt + 1}");
            using var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
            {
                Email = "unknown-proxy-test@example.com",
                Password = "password123"
            });
            actual = response.StatusCode;
        }

        actual.Should().Be(expected);
    }
}
