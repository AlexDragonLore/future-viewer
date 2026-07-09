using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.Integration.Tests.Fixtures;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class AnnouncementEndpointTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public AnnouncementEndpointTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Unread_returns_seeded_current_release_announcement()
    {
        var client = await CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/announcements/unread");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var announcements = await response.Content.ReadFromJsonAsync<List<AnnouncementDto>>();
        announcements.Should().NotBeNull();
        announcements!.Should().Contain(a => a.Code == "current-release-validation-history-announcements");
    }

    [Fact]
    public async Task Mark_read_removes_announcement_from_unread()
    {
        var client = await CreateAuthenticatedClient();
        var first = await client.GetFromJsonAsync<List<AnnouncementDto>>("/api/announcements/unread");
        var announcement = first!.Single(a => a.Code == "current-release-validation-history-announcements");

        var markResponse = await client.PostAsync($"/api/announcements/{announcement.Id}/read", null);
        var second = await client.GetFromJsonAsync<List<AnnouncementDto>>("/api/announcements/unread");

        markResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        second!.Select(a => a.Id).Should().NotContain(announcement.Id);
    }

    [Fact]
    public async Task Unread_without_token_returns_unauthorized()
    {
        var client = _fixture.CreateClient();
        var response = await client.GetAsync("/api/announcements/unread");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<HttpClient> CreateAuthenticatedClient()
    {
        var client = _fixture.CreateClient();
        var email = $"ann-{Guid.NewGuid():N}@example.com";
        var auth = await _fixture.RegisterAndLoginAsync(client, email, "password123");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}
