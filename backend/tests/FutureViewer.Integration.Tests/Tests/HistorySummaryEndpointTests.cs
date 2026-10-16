using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.Infrastructure.Persistence;
using FutureViewer.Infrastructure.Persistence.Repositories;
using FutureViewer.Integration.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class HistorySummaryEndpointTests(IntegrationTestFixture fixture) : IClassFixture<IntegrationTestFixture>
{
    [Fact]
    public async Task Summary_pages_all_owned_visible_history_and_returns_only_a_bounded_preview()
    {
        var (client, auth) = await Owner();
        var (_, foreign) = await Owner();
        var now = DateTime.UtcNow;
        var body = new string('a', 1000) + "BODY-TAIL-MUST-NOT-LEAK";
        var readings = Enumerable.Range(1, 61).Select(i => new Reading
        {
            Id = Guid.Parse($"33333333-0000-0000-0000-{i:D12}"), UserId = auth.UserId,
            Question = $"Вопрос {i} " + new string('я', 300), AiInterpretation = body,
            SpreadType = SpreadType.CelticCross, SavedToHistory = true, CreatedAt = now
        }).ToArray();
        var hidden = Reading(auth.UserId, "Скрытый");
        hidden.DeletedFromHistoryAt = now;
        var unsaved = Reading(auth.UserId, "Несохранённый");
        unsaved.SavedToHistory = false;
        var other = Reading(foreign.UserId, "Чужой");
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Readings.AddRange(readings);
            db.Readings.AddRange(hidden, unsaved, other, Reading(null, "Гостевой"));
            await db.SaveChangesAsync();
        }

        var seen = new List<Guid>();
        for (var page = 1; page <= 4; page++)
        {
            var response = await client.GetAsync($"/api/readings/history/summary?page={page}&userId={foreign.UserId}");
            response.EnsureSuccessStatusCode();
            response.Headers.CacheControl!.NoStore.Should().BeTrue();
            var json = await response.Content.ReadAsStringAsync();
            json.Should().NotContain("BODY-TAIL-MUST-NOT-LEAK");
            using var document = JsonDocument.Parse(json);
            foreach (var item in document.RootElement.GetProperty("items").EnumerateArray())
            {
                item.TryGetProperty("cards", out _).Should().BeFalse();
                item.TryGetProperty("interpretation", out _).Should().BeFalse();
            }
            var result = JsonSerializer.Deserialize<ReadingHistoryPageDto>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            result.Total.Should().Be(61);
            result.Page.Should().Be(page);
            result.PageSize.Should().Be(20);
            result.Items.Should().HaveCount(page == 4 ? 1 : 20);
            result.Items.Should().OnlyContain(r => r.InterpretationPreview!.Length == ReadingHistorySummaryDto.PreviewLength
                && r.Question.Length > 300 && r.SpreadName.Length > 0);
            seen.AddRange(result.Items.Select(r => r.Id));
        }
        seen.Should().Equal(readings.Reverse().Select(r => r.Id));
        (await client.DeleteAsync($"/api/readings/{other.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync($"/api/readings/{other.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Removing the sole row on page four leaves sixty visible records; the UI can refresh page three.
        (await client.DeleteAsync($"/api/readings/{seen[^1]}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var emptyLast = (await client.GetFromJsonAsync<ReadingHistoryPageDto>("/api/readings/history/summary?page=4"))!;
        emptyLast.Total.Should().Be(60);
        emptyLast.Items.Should().BeEmpty();
        (await client.GetFromJsonAsync<ReadingHistoryPageDto>("/api/readings/history/summary?page=3"))!.Items.Should().HaveCount(20);
        (await client.GetFromJsonAsync<ReadingResult[]>("/api/readings/history"))!.Should().HaveCount(50);
    }

    [Fact]
    public async Task Summary_caps_and_normalizes_pagination_and_requires_authentication()
    {
        (await fixture.CreateClient().GetAsync("/api/readings/history/summary")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var (client, auth) = await Owner();
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Readings.AddRange(Enumerable.Range(0, 105).Select(i => Reading(auth.UserId, $"Вопрос {i}")));
            await db.SaveChangesAsync();
        }
        var capped = (await client.GetFromJsonAsync<ReadingHistoryPageDto>("/api/readings/history/summary?page=1&pageSize=1000"))!;
        capped.PageSize.Should().Be(100);
        capped.Items.Should().HaveCount(100);
        var normalized = (await client.GetFromJsonAsync<ReadingHistoryPageDto>("/api/readings/history/summary?page=0&pageSize=0"))!;
        normalized.Page.Should().Be(1);
        normalized.PageSize.Should().Be(1);
        var beyond = (await client.GetFromJsonAsync<ReadingHistoryPageDto>($"/api/readings/history/summary?page={int.MaxValue}&pageSize=100"))!;
        beyond.Items.Should().BeEmpty();
        beyond.Total.Should().Be(105);
    }

    [Fact]
    public async Task Summary_projects_preview_in_sql_without_cards_or_full_body_and_uses_two_queries()
    {
        var (_, auth) = await Owner();
        using var scope = fixture.Services.CreateScope();
        var original = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var longReading = new Reading
        {
            UserId = auth.UserId, Question = "Длинный ответ", SpreadType = SpreadType.SingleCard,
            SavedToHistory = true, AiInterpretation = new string('a', 10_000)
        };
        original.Readings.AddRange(longReading, Reading(auth.UserId, "Короткий ответ"));
        await original.SaveChangesAsync();
        var capture = new QueryCapture();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(original.Database.GetConnectionString()).AddInterceptors(capture).Options);
        var repository = new ReadingRepository(db);
        var page = await repository.GetHistorySummaryAsync(auth.UserId, 0, 20);
        (await repository.CountHistoryAsync(auth.UserId)).Should().Be(2);
        page.Single(r => r.Id == longReading.Id).InterpretationPreview.Should().HaveLength(ReadingHistorySummaryDto.PreviewLength);
        page.Single(r => r.Id != longReading.Id).InterpretationPreview.Should().Be("Коротко");
        capture.Commands.Should().HaveCount(2);
        capture.Commands.Should().OnlyContain(sql => !sql.Contains("reading_cards") && !sql.Contains("tarot_cards"));
        capture.Commands[0].Should().MatchRegex("(?i)substr(?:ing)?\\(");
        capture.Fields.Should().NotContain("ai_interpretation").And.NotContain("Interpretation");
        db.ChangeTracker.Entries().Should().BeEmpty();
    }

    private static Reading Reading(Guid? owner, string question) => new()
    {
        UserId = owner, Question = question, SpreadType = SpreadType.SingleCard,
        AiInterpretation = "Коротко", SavedToHistory = true
    };

    private async Task<(HttpClient Client, AuthResponse Auth)> Owner()
    {
        var client = fixture.CreateClient();
        var auth = await fixture.RegisterAndLoginAsync(client, $"history-summary-{Guid.NewGuid():N}@example.com", "password123");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth);
    }

    private sealed class QueryCapture : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];
        public List<string> Fields { get; } = [];
        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            Fields.AddRange(Enumerable.Range(0, result.FieldCount).Select(result.GetName));
            return new ValueTask<DbDataReader>(result);
        }
    }
}
