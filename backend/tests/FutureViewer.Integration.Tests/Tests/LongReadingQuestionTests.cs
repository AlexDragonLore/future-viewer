using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.Host.Auth;
using FutureViewer.Infrastructure.Persistence;
using FutureViewer.Integration.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class LongReadingQuestionTests(IntegrationTestFixture fixture) : IClassFixture<IntegrationTestFixture>
{
    [Theory]
    [InlineData("/api/readings", 20_000)]
    [InlineData("/api/readings/stream", 20_000)]
    [InlineData("/api/readings/validate-question", 20_000)]
    [InlineData("/api/readings/guest", 20_000)]
    [InlineData("/api/readings", 20_001)]
    [InlineData("/api/readings/stream", 20_001)]
    [InlineData("/api/readings/validate-question", 20_001)]
    [InlineData("/api/readings/guest", 20_001)]
    public async Task Reading_endpoints_accept_the_limit_and_reject_larger_questions(string endpoint, int length)
    {
        var client = fixture.CreateClient();
        var isGuest = endpoint.EndsWith("/guest", StringComparison.Ordinal);
        if (!isGuest)
        {
            var auth = await fixture.RegisterAndLoginAsync(client, $"long-question-{Guid.NewGuid():N}@example.com", "password123");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        }
        var question = string.Concat(Enumerable.Repeat("Как развивать мой творческий проект? ", 600))[..(length - 1)] + "?";
        var request = new CreateReadingRequest
        {
            SpreadType = isGuest ? SpreadType.ThreeCard : SpreadType.SingleCard,
            Question = question
        };

        var response = await client.PostAsJsonAsync(endpoint, request);

        if (length > 20_000)
        {
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            return;
        }

        response.EnsureSuccessStatusCode();
        if (endpoint.EndsWith("/validate-question", StringComparison.Ordinal))
        {
            var result = await response.Content.ReadFromJsonAsync<QuestionValidationCheckDto>();
            result!.CanContinue.Should().BeTrue();
            return;
        }
        ReadingResult reading;
        if (endpoint.EndsWith("/stream", StringComparison.Ordinal))
        {
            var frames = (await response.Content.ReadAsStringAsync()).Split('\n', StringSplitOptions.RemoveEmptyEntries);
            using var lastFrame = JsonDocument.Parse(frames[^1]);
            lastFrame.RootElement.GetProperty("type").GetString().Should().Be("done");
            using var cardsFrame = JsonDocument.Parse(frames[0]);
            reading = cardsFrame.RootElement.GetProperty("reading")
                .Deserialize<ReadingResult>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        }
        else if (isGuest)
        {
            var guest = (await response.Content.ReadFromJsonAsync<GuestReadingResponse>())!;
            reading = guest.Reading;
            var ticketRequest = new GuestReadingTicketRequest(guest.Ticket);
            var preview = await client.PostAsJsonAsync("/api/readings/guest/preview", ticketRequest);
            preview.EnsureSuccessStatusCode();
            (await preview.Content.ReadFromJsonAsync<ReadingResult>())!.Question.Should().Be(question);

            var auth = await fixture.RegisterAndLoginAsync(client, $"long-guest-{Guid.NewGuid():N}@example.com", "password123");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
            var unlock = await client.PostAsJsonAsync("/api/readings/guest/unlock", ticketRequest);
            unlock.EnsureSuccessStatusCode();
            (await unlock.Content.ReadFromJsonAsync<ReadingResult>())!.Question.Should().Be(question);
        }
        else
        {
            reading = (await response.Content.ReadFromJsonAsync<ReadingResult>())!;
        }
        reading.Question.Should().Be(question);

        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Readings.AsNoTracking().SingleAsync(r => r.Id == reading.Id)).Question.Should().Be(question);
    }
}
