using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Interfaces;
using FutureViewer.Host.Auth;
using FutureViewer.Integration.Tests.Fixtures;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class GuestReadingEndpointTests(IntegrationTestFixture fixture) : IClassFixture<IntegrationTestFixture>
{
    private static CreateReadingRequest Request(SpreadType spread = SpreadType.SingleCard) => new()
    {
        SpreadType = spread, Question = "На что мне сейчас стоит обратить внимание?", SaveToHistory = true
    };

    private async Task<GuestReadingResponse> CreateGuest(HttpClient? client = null)
    {
        var response = await (client ?? fixture.CreateClient()).PostAsJsonAsync("/api/readings/guest", Request());
        response.EnsureSuccessStatusCode();
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        return (await response.Content.ReadFromJsonAsync<GuestReadingResponse>())!;
    }

    private async Task<HttpClient> Login()
    {
        var client = fixture.CreateClient();
        var auth = await fixture.RegisterAndLoginAsync(client, $"guest-{Guid.NewGuid():N}@example.com", "password123");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    [Fact]
    public async Task Guest_gets_one_card_and_only_half_the_interpretation_then_resumes_same_result()
    {
        var guest = await CreateGuest();
        guest.Reading.Cards.Should().ContainSingle();
        guest.Reading.IsPreview.Should().BeTrue();
        guest.ExpiresAt.Should().BeCloseTo(DateTimeOffset.UtcNow.AddHours(24), TimeSpan.FromSeconds(10));
        guest.Ticket.Should().NotContain("Stub interpretation");

        var anonymous = fixture.CreateClient();
        var request = new GuestReadingTicketRequest(guest.Ticket);
        var preview = await (await anonymous.PostAsJsonAsync("/api/readings/guest/preview", request))
            .Content.ReadFromJsonAsync<ReadingResult>();
        preview.Should().BeEquivalentTo(guest.Reading);
        (await anonymous.PostAsJsonAsync("/api/readings/guest/unlock", request)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.GetAsync($"/api/readings/{guest.Reading.Id}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var owner = await Login();
        var response = await owner.PostAsJsonAsync("/api/readings/guest/unlock", request);
        response.EnsureSuccessStatusCode();
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        var full = (await response.Content.ReadFromJsonAsync<ReadingResult>())!;
        full.IsPreview.Should().BeFalse();
        full.Id.Should().Be(guest.Reading.Id);
        full.Cards.Should().BeEquivalentTo(guest.Reading.Cards);
        full.Interpretation.Should().StartWith(guest.Reading.Interpretation![..^1]);
        guest.Reading.Interpretation.Length.Should().BeLessThanOrEqualTo(full.Interpretation!.Length / 2 + 1);

        // Idempotent resume costs no additional reading and never silently opts into history.
        (await owner.PostAsJsonAsync("/api/readings/guest/unlock", request)).EnsureSuccessStatusCode();
        (await (await owner.GetAsync("/api/readings/history")).Content.ReadFromJsonAsync<ReadingResult[]>()).Should().BeEmpty();
        using var scope = fixture.Services.CreateScope();
        var readings = scope.ServiceProvider.GetRequiredService<IReadingRepository>();
        var stored = (await readings.GetByIdAsync(full.Id))!;
        stored.Question.Should().BeEmpty();
        stored.AiInterpretation.Should().BeNull();
        stored.SavedToHistory.Should().BeFalse();
        (await readings.CountByUserAsync(stored.UserId!.Value)).Should().Be(1);
    }

    [Fact]
    public async Task Guest_rejects_other_spreads_and_personal_data()
    {
        var client = fixture.CreateClient();
        (await client.PostAsJsonAsync("/api/readings/guest", Request(SpreadType.ThreeCard)))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await client.PostAsJsonAsync("/api/readings/guest", new CreateReadingRequest
        {
            SpreadType = SpreadType.SingleCard, Question = "Напиши ответ для ivan@example.com"
        })).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Guest_creation_is_rate_limited_per_client()
    {
        var client = fixture.CreateClient();
        for (var i = 0; i < 3; i++) await CreateGuest(client);
        (await client.PostAsJsonAsync("/api/readings/guest", Request())).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        await CreateGuest(); // Another client has its own allowance.
    }

    [Fact]
    public async Task Ticket_cannot_be_claimed_by_two_accounts_even_concurrently()
    {
        var guest = await CreateGuest();
        var first = await Login();
        var second = await Login();
        var request = new GuestReadingTicketRequest(guest.Ticket);
        var responses = await Task.WhenAll(
            first.PostAsJsonAsync("/api/readings/guest/unlock", request),
            second.PostAsJsonAsync("/api/readings/guest/unlock", request));
        responses.Select(r => r.StatusCode).Should().BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.NotFound]);
    }

    [Theory]
    [InlineData("tampered")]
    [InlineData("expired")]
    public async Task Invalid_ticket_cannot_restore_preview_or_unlock(string kind)
    {
        var guest = await CreateGuest();
        var ticket = guest.Ticket[..^5] + "xxxxx";
        if (kind == "expired")
        {
            var provider = fixture.Services.GetRequiredService<IDataProtectionProvider>();
            ticket = provider.CreateProtector("FutureViewer.GuestReading.v1").ToTimeLimitedDataProtector()
                .Protect(JsonSerializer.Serialize(guest.Reading), DateTimeOffset.UtcNow.AddMinutes(-1));
        }
        var request = new GuestReadingTicketRequest(ticket);
        (await fixture.CreateClient().PostAsJsonAsync("/api/readings/guest/preview", request))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await (await Login()).PostAsJsonAsync("/api/readings/guest/unlock", request))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
