using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Interfaces;
using FutureViewer.Host.Auth;
using FutureViewer.Infrastructure.Persistence;
using FutureViewer.Integration.Tests.Fixtures;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class GuestReadingEndpointTests(IntegrationTestFixture fixture) : IClassFixture<IntegrationTestFixture>
{
    private static CreateReadingRequest Request(SpreadType spread = SpreadType.ThreeCard) => new()
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
    public async Task Guest_gets_three_cards_and_only_half_the_interpretation_then_resumes_same_result()
    {
        var guest = await CreateGuest();
        guest.Reading.Cards.Should().HaveCount(3);
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

        using (var anonymousScope = fixture.Services.CreateScope())
        {
            var anonymousReading = (await anonymousScope.ServiceProvider.GetRequiredService<IReadingRepository>()
                .GetByIdAsync(guest.Reading.Id))!;
            anonymousReading.UserId.Should().BeNull();
            anonymousReading.SavedToHistory.Should().BeFalse();
            anonymousReading.Question.Should().Be(Request().Question);
            anonymousReading.AiInterpretation.Should().NotBeNullOrWhiteSpace();
            anonymousReading.AiInterpretation!.Length.Should().BeGreaterThan(guest.Reading.Interpretation!.Length);
        }

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

        // Idempotent resume saves the same full reading without consuming another draw.
        (await owner.PostAsJsonAsync("/api/readings/guest/unlock", request)).EnsureSuccessStatusCode();
        var history = (await (await owner.GetAsync("/api/readings/history")).Content.ReadFromJsonAsync<ReadingResult[]>())!;
        var historyReading = history.Should().ContainSingle().Which;
        historyReading.Should().BeEquivalentTo(full, options => options.Excluding(reading => reading.CreatedAt));
        historyReading.CreatedAt.Should().BeCloseTo(full.CreatedAt, TimeSpan.FromMicroseconds(1),
            "PostgreSQL timestamps have microsecond precision");
        var detail = await (await owner.GetAsync($"/api/readings/{full.Id}")).Content.ReadFromJsonAsync<ReadingResult>();
        detail.Should().BeEquivalentTo(full, options => options.Excluding(reading => reading.CreatedAt));
        detail!.CreatedAt.Should().Be(historyReading.CreatedAt);
        using var scope = fixture.Services.CreateScope();
        var readings = scope.ServiceProvider.GetRequiredService<IReadingRepository>();
        var stored = (await readings.GetByIdAsync(full.Id))!;
        stored.Question.Should().Be(full.Question);
        stored.AiInterpretation.Should().Be(full.Interpretation);
        stored.SavedToHistory.Should().BeTrue();
        (await readings.CountByUserAsync(stored.UserId!.Value)).Should().Be(1);
        var status = (await (await owner.GetAsync("/api/subscription/status")).Content.ReadFromJsonAsync<SubscriptionStatusDto>())!;
        status.CanCreateIntroReading.Should().BeFalse();
        status.CanCreateFreeReading.Should().BeFalse();
        (await owner.PostAsJsonAsync("/api/readings", Request())).StatusCode.Should().Be(HttpStatusCode.PaymentRequired);
        (await owner.PostAsJsonAsync("/api/readings", Request(SpreadType.SingleCard))).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Guest_ticket_expires_twenty_four_hours_after_creation()
    {
        var guest = await CreateGuest();
        guest.ExpiresAt.Should().Be(new DateTimeOffset(guest.Reading.CreatedAt, TimeSpan.Zero).AddHours(24));
    }

    [Fact]
    public async Task Expired_guest_row_cannot_be_claimed_even_before_cleanup()
    {
        var guest = await CreateGuest();
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE readings SET created_at = {DateTime.UtcNow.AddHours(-24)} WHERE id = {guest.Reading.Id}");

        var owner = await Login();
        var response = await owner.PostAsJsonAsync("/api/readings/guest/unlock", new GuestReadingTicketRequest(guest.Ticket));
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Unlock_does_not_restore_a_reading_removed_from_history()
    {
        var guest = await CreateGuest();
        var owner = await Login();
        var request = new GuestReadingTicketRequest(guest.Ticket);
        (await owner.PostAsJsonAsync("/api/readings/guest/unlock", request)).EnsureSuccessStatusCode();
        (await owner.DeleteAsync($"/api/readings/{guest.Reading.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await owner.PostAsJsonAsync("/api/readings/guest/unlock", request)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await (await owner.GetAsync("/api/readings/history")).Content.ReadFromJsonAsync<ReadingResult[]>()).Should().BeEmpty();
    }

    [Fact]
    public async Task Guest_rejects_other_spreads_and_personal_data()
    {
        var client = fixture.CreateClient();
        (await client.PostAsJsonAsync("/api/readings/guest", Request(SpreadType.CelticCross)))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await client.PostAsJsonAsync("/api/readings/guest", new CreateReadingRequest
        {
            SpreadType = SpreadType.SingleCard, Question = "Напиши ответ для ivan@example.com"
        })).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Existing_single_card_guest_tickets_can_still_be_claimed()
    {
        var response = await fixture.CreateClient().PostAsJsonAsync("/api/readings/guest", Request(SpreadType.SingleCard));
        response.EnsureSuccessStatusCode();
        var guest = (await response.Content.ReadFromJsonAsync<GuestReadingResponse>())!;
        guest.Reading.Cards.Should().ContainSingle();
        var owner = await Login();

        (await owner.PostAsJsonAsync("/api/readings/guest/unlock", new GuestReadingTicketRequest(guest.Ticket)))
            .EnsureSuccessStatusCode();
        (await owner.PostAsJsonAsync("/api/readings", Request())).StatusCode.Should().Be(HttpStatusCode.PaymentRequired);
    }

    [Fact]
    public async Task One_account_cannot_claim_two_intro_readings_even_concurrently()
    {
        var first = await CreateGuest();
        var second = await CreateGuest();
        var owner = await Login();
        var responses = await Task.WhenAll(
            owner.PostAsJsonAsync("/api/readings/guest/unlock", new GuestReadingTicketRequest(first.Ticket)),
            owner.PostAsJsonAsync("/api/readings/guest/unlock", new GuestReadingTicketRequest(second.Ticket)));

        responses.Select(r => r.StatusCode).Should().BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.PaymentRequired]);
        var history = await (await owner.GetAsync("/api/readings/history")).Content.ReadFromJsonAsync<ReadingResult[]>();
        history.Should().ContainSingle();
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
