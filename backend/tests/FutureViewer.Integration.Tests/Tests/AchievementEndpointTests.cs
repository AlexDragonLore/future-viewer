using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.Integration.Tests.Fixtures;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class AchievementEndpointTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public AchievementEndpointTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Achievements_catalog_is_anonymous_and_lists_seeded_items()
    {
        var client = _fixture.CreateClient();

        var response = await client.GetAsync("/api/achievements");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<List<AchievementCatalogItem>>();
        list.Should().NotBeNull();
        list!.Should().HaveCount(12);
        list.Select(a => a.Code).Should().Contain("first_reading");
    }

    [Fact]
    public async Task Achievements_me_requires_authentication()
    {
        var client = _fixture.CreateClient();
        var response = await client.GetAsync("/api/achievements/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Achievements_me_returns_catalog_with_unlocked_status()
    {
        var client = _fixture.CreateClient();
        var email = $"ach-{Guid.NewGuid():N}@example.com";

        var register = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Email = email, Password = "password123" });
        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var response = await client.GetAsync("/api/achievements/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<List<AchievementDto>>();
        list.Should().NotBeNull();
        list!.Should().HaveCount(12);
        list.Should().OnlyContain(a => a.UnlockedAt == null);
    }

    private sealed class AchievementCatalogItem
    {
        public Guid Id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string IconPath { get; init; } = string.Empty;
        public int SortOrder { get; init; }
    }
}
