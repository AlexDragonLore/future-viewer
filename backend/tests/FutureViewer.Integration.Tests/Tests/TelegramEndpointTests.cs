using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.Integration.Tests.Fixtures;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class TelegramEndpointTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public TelegramEndpointTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Link_endpoint_requires_authentication()
    {
        var client = _fixture.CreateClient();
        var response = await client.PostAsync("/api/telegram/link", content: null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Status_endpoint_requires_authentication()
    {
        var client = _fixture.CreateClient();
        var response = await client.GetAsync("/api/telegram/status");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Status_returns_not_linked_for_new_user()
    {
        var client = await CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/telegram/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TelegramStatusResponse>();
        body!.IsLinked.Should().BeFalse();
    }

    [Fact]
    public async Task Link_generates_deeplink_for_new_user()
    {
        var client = await CreateAuthenticatedClient();

        var response = await client.PostAsync("/api/telegram/link", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TelegramLinkResponse>();
        body.Should().NotBeNull();
        body!.IsLinked.Should().BeFalse();
        body.DeepLinkUrl.Should().StartWith("https://t.me/");
    }

    [Fact]
    public async Task Unlink_succeeds_and_is_idempotent()
    {
        var client = await CreateAuthenticatedClient();

        var response = await client.DeleteAsync("/api/telegram/link");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private async Task<HttpClient> CreateAuthenticatedClient()
    {
        var client = _fixture.CreateClient();
        var email = $"tg-{Guid.NewGuid():N}@example.com";

        var auth = await _fixture.RegisterAndLoginAsync(client, email, "password123");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    private sealed class TelegramStatusResponse
    {
        public bool IsLinked { get; init; }
    }
}
