using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using FutureViewer.Integration.Tests.Fixtures;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class BirthdayEndpointTests(IntegrationTestFixture fixture)
    : IClassFixture<IntegrationTestFixture>
{
    [Theory]
    [InlineData("/api/birthday")]
    [InlineData("/api/birthday/unlock")]
    [InlineData("/api/birthday/session")]
    [InlineData("/api/birthday/answer")]
    [InlineData("/api/birthday/gift")]
    [InlineData("/api/birthday/assets/certificate")]
    [InlineData("/api/birthday/assets/postcard")]
    public async Task Retired_invitation_rejects_old_keys_tickets_and_direct_asset_access(string path)
    {
        using var client = fixture.CreateClient();
        using var post = await client.PostAsJsonAsync(path, new { key = "old-key", ticket = "old-ticket", score = 500 });
        using var get = await client.GetAsync(path);
        foreach (var response in new[] { post, get })
        {
            response.StatusCode.Should().Be(HttpStatusCode.Gone);
            response.Headers.CacheControl!.NoStore.Should().BeTrue();
            response.Headers.GetValues("X-Robots-Tag").Should().ContainSingle("noindex, nofollow, noarchive");
            (await response.Content.ReadAsByteArrayAsync()).Should().BeEmpty();
        }
    }
}
