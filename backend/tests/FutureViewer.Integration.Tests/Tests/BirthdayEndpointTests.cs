using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using FutureViewer.Host.Auth;
using FutureViewer.Integration.Tests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class BirthdayEndpointTests : IClassFixture<IntegrationTestFixture>, IDisposable
{
    private const string Key = "test-invitation-only-never-a-real-key";
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"birthday-test-{Guid.NewGuid():N}");
    private readonly WebApplicationFactory<Program> _factory;
    private static readonly (string Question, string Answer)[] Answers =
        [("foundation", "soil"), ("plan", "routes"), ("light", "daylight"), ("garden", "plants"), ("home", "life")];

    public BirthdayEndpointTests(IntegrationTestFixture fixture)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllBytes(Path.Combine(_directory, "certificate.jpg"), [0xff, 0xd8, 0xff, 0xd9]);
        File.WriteAllBytes(Path.Combine(_directory, "postcard.jpg"), [0xff, 0xd8, 0xff, 0xd9]);
        _factory = fixture.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Birthday:KeyHash"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Key))),
                ["Birthday:CertificatePath"] = Path.Combine(_directory, "certificate.jpg"),
                ["Birthday:PostcardPath"] = Path.Combine(_directory, "postcard.jpg")
            })));
    }

    private static async Task<BirthdaySession> Unlock(HttpClient client)
    {
        var result = await client.PostAsJsonAsync("/api/birthday/unlock", new { key = Key });
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Headers.CacheControl!.NoStore.Should().BeTrue();
        return (await result.Content.ReadFromJsonAsync<BirthdaySession>())!;
    }

    [Fact]
    public async Task Requires_key_and_does_not_expose_answers_or_gift()
    {
        using var client = _factory.CreateClient();
        foreach (var key in new string?[] { null, "", "wrong", new('a', 257) })
            (await client.PostAsJsonAsync("/api/birthday/unlock", new { key })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var state = await Unlock(client);
        state.Score.Should().Be(0);
        state.Question!.Id.Should().Be("foundation");
        var serialized = JsonSerializer.Serialize(state);
        serialized.Should().NotContain("15000").And.NotContain("certificate").And.NotContain(Key);
        (await client.PostAsJsonAsync("/api/birthday/gift", new { state.Ticket })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        foreach (var kind in new[] { "certificate", "postcard" })
            (await client.PostAsJsonAsync($"/api/birthday/assets/{kind}", new { state.Ticket })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Invalid_and_expired_tickets_cannot_read_progress_or_assets()
    {
        using var client = _factory.CreateClient();
        var expired = _factory.Services.GetRequiredService<BirthdayAccess>().Issue(5, DateTimeOffset.UtcNow.AddMinutes(-1));
        foreach (var ticket in new string?[] { null, "forged", expired.Ticket, new('x', 5000) })
        foreach (var endpoint in new[] { "/session", "/gift", "/assets/certificate", "/assets/postcard" })
            (await client.PostAsJsonAsync("/api/birthday" + endpoint, new { ticket })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Valid_ticket_cannot_be_tampered_to_complete_the_game()
    {
        using var client = _factory.CreateClient();
        var state = await Unlock(client);
        var ticket = state.Ticket[..20] + (state.Ticket[20] == 'A' ? "B" : "A") + state.Ticket[21..];
        (await client.PostAsJsonAsync("/api/birthday/gift", new { ticket, score = 500, completedSteps = 5 }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Wrong_answer_gives_hint_and_retries_do_not_inflate_score()
    {
        using var client = _factory.CreateClient();
        var state = await Unlock(client);
        var wrong = await client.PostAsJsonAsync("/api/birthday/answer",
            new { state.Ticket, questionId = "foundation", optionId = "facade", score = 500 });
        var hint = (await wrong.Content.ReadFromJsonAsync<AnswerPayload>())!;
        hint.Correct.Should().BeFalse();
        hint.Message.Should().NotBeNullOrEmpty();
        hint.Session.Score.Should().Be(0);
        for (var i = 0; i < 2; i++)
        {
            var answer = await client.PostAsJsonAsync("/api/birthday/answer",
                new { state.Ticket, questionId = "foundation", optionId = "soil" });
            (await answer.Content.ReadFromJsonAsync<AnswerPayload>())!.Session.Score.Should().Be(100);
        }
    }

    [Fact]
    public async Task Cannot_skip_stage_or_submit_an_unknown_option()
    {
        using var client = _factory.CreateClient();
        var state = await Unlock(client);
        foreach (var (questionId, optionId) in new[] { ("home", "life"), ("foundation", "unknown") })
            (await client.PostAsJsonAsync("/api/birthday/answer", new { state.Ticket, questionId, optionId }))
                .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task All_five_stages_unlock_original_assets_and_support_resume()
    {
        using var client = _factory.CreateClient();
        var state = await Unlock(client);
        foreach (var (questionId, optionId) in Answers)
        {
            var answer = await client.PostAsJsonAsync("/api/birthday/answer", new { state.Ticket, questionId, optionId });
            var result = (await answer.Content.ReadFromJsonAsync<AnswerPayload>())!;
            result.Correct.Should().BeTrue();
            result.Session.Score.Should().Be(state.Score + 100);
            state = result.Session;
        }
        state.Question.Should().BeNull();
        var resume = await client.PostAsJsonAsync("/api/birthday/session", new { state.Ticket });
        (await resume.Content.ReadFromJsonAsync<BirthdaySession>())!.Score.Should().Be(500);
        var gift = await client.PostAsJsonAsync("/api/birthday/gift", new { state.Ticket });
        (await gift.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("amount").GetInt32().Should().Be(15000);
        foreach (var kind in new[] { "certificate", "postcard" })
        {
            var file = await client.PostAsJsonAsync($"/api/birthday/assets/{kind}", new { state.Ticket });
            file.StatusCode.Should().Be(HttpStatusCode.OK);
            file.Content.Headers.ContentType!.MediaType.Should().Be("image/jpeg");
            file.Headers.CacheControl!.NoStore.Should().BeTrue();
            file.Headers.GetValues("X-Robots-Tag").Should().ContainSingle("noindex, nofollow, noarchive");
            (await file.Content.ReadAsByteArrayAsync()).Should().Equal([0xff, 0xd8, 0xff, 0xd9]);
        }
        (await Unlock(client)).Score.Should().Be(0); // Another visit never consumes the gift.
    }

    [Fact]
    public async Task Rotating_the_key_invalidates_existing_tickets()
    {
        using var client = _factory.CreateClient();
        var state = await Unlock(client);
        using var rotated = _factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["Birthday:KeyHash"] = new('a', 64) })));
        using var other = rotated.CreateClient();
        (await other.PostAsJsonAsync("/api/birthday/session", new { state.Ticket })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Missing_assets_disable_invitation_and_brute_force_is_limited()
    {
        using var client = _factory.CreateClient();
        for (var i = 0; i < 10; i++)
            (await client.PostAsJsonAsync("/api/birthday/unlock", new { key = "wrong" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PostAsJsonAsync("/api/birthday/unlock", new { key = Key })).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        File.Delete(Path.Combine(_directory, "certificate.jpg"));
        using var disabled = _factory.WithWebHostBuilder(_ => { });
        using var other = disabled.CreateClient();
        (await other.PostAsJsonAsync("/api/birthday/unlock", new { key = Key })).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    public void Dispose()
    {
        _factory.Dispose();
        Directory.Delete(_directory, true);
    }
    private sealed record AnswerPayload(bool Correct, string Message, BirthdaySession Session);
}
