using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace FutureViewer.Integration.Tests.Tests;

public sealed partial class AdminReadingEndpointTests
{
    [Fact]
    public async Task Views_are_personal_idempotent_and_only_acknowledge_the_displayed_batch()
    {
        var (admin, actor) = await CreateClient(admin: true);
        var (otherAdmin, _) = await CreateClient(admin: true);
        var (_, owner) = await CreateClient();
        var rows = Enumerable.Range(1, 5).Select(i => new Reading
        {
            UserId = owner.UserId, SavedToHistory = true, SpreadType = SpreadType.SingleCard,
            Question = $"Review {i}", CreatedAt = DateTime.UtcNow.AddDays(-i)
        }).ToArray();
        await Seed(rows);
        var query = $"userId={owner.UserId}&unseenOnly=true&sort=oldest&pageSize=2";
        var first = await Read(admin, query);
        first.Total.Should().Be(5);
        first.Items.Select(r => r.Id).Should().Equal(rows[4].Id, rows[3].Id);
        (await Read(admin, query)).Total.Should().Be(5); // GET is read-only.
        var body = new { readingIds = first.Items.Select(r => r.Id).Append(first.Items[0].Id).ToArray(), adminId = owner.UserId };
        var responses = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => admin.PostAsJsonAsync("/api/admin/readings/viewed", body)));
        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.NoContent);
        var next = await Read(admin, query);
        next.Total.Should().Be(3);
        next.Items.Select(r => r.Id).Should().Equal(rows[2].Id, rows[1].Id);
        (await Read(otherAdmin, query)).Total.Should().Be(5);
        (await Read(admin, $"userId={owner.UserId}&sort=newest")).Items.Select(r => r.Id)
            .Should().Equal(rows.Select(r => r.Id));
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var receipts = db.AdminReadingViews.Where(v => v.AdminId == actor.UserId).ToArray();
        receipts.Should().HaveCount(2);
        receipts.Select(v => v.ReadingId).Should().BeEquivalentTo(first.Items.Select(r => r.Id));
    }

    [Fact]
    public async Task Views_ignore_unretained_content_and_sort_stably_with_search_and_audience()
    {
        var (admin, actor) = await CreateClient(admin: true);
        var (_, owner) = await CreateClient();
        var marker = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;
        var guest = new Reading { Question = marker, SpreadType = SpreadType.SingleCard, CreatedAt = now.AddMinutes(-1) };
        var expired = new Reading { Question = marker, SpreadType = SpreadType.SingleCard, CreatedAt = now.AddDays(-31) };
        var privateRow = Reading(owner.UserId, marker, null);
        privateRow.SavedToHistory = false;
        var sameTime = Enumerable.Range(0, 3).Select(_ => new Reading
        {
            UserId = owner.UserId, Question = marker, SavedToHistory = true,
            SpreadType = SpreadType.SingleCard, CreatedAt = now
        }).OrderBy(r => r.Id).ToArray();
        await Seed([guest, expired, privateRow, .. sameTime]);
        for (var i = 0; i < 3; i++)
            (await Read(admin, $"search={marker}&audience=registered&sort=oldest&pageSize=1&page={i+1}&unseenOnly=true"))
                .Items.Should().ContainSingle().Which.Id.Should().Be(sameTime[i].Id);
        (await admin.PostAsJsonAsync("/api/admin/readings/viewed", new { readingIds = new[] { guest.Id, expired.Id, privateRow.Id, Guid.NewGuid() } })).EnsureSuccessStatusCode();
        (await Read(admin, $"search={marker}&audience=guests&unseenOnly=true")).Total.Should().Be(0);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.AdminReadingViews.Where(v => v.AdminId == actor.UserId).Should().ContainSingle().Which.ReadingId.Should().Be(guest.Id);
        db.Readings.Remove((await db.Readings.FindAsync(guest.Id))!);
        await db.SaveChangesAsync();
        db.AdminReadingViews.Count(v => v.AdminId == actor.UserId).Should().Be(0);
    }

    [Fact]
    public async Task Views_require_admin_and_validate_batches_and_sort()
    {
        var (user, _) = await CreateClient();
        var (admin, _) = await CreateClient(admin: true);
        var body = new { readingIds = new[] { Guid.NewGuid() } };
        (await fixture.CreateClient().PostAsJsonAsync("/api/admin/readings/viewed", body)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await user.PostAsJsonAsync("/api/admin/readings/viewed", body)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        foreach (var ids in new Guid[]?[] { null, [], Enumerable.Range(0, 101).Select(_ => Guid.NewGuid()).ToArray() })
            (await admin.PostAsJsonAsync("/api/admin/readings/viewed", new { readingIds = ids })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.GetAsync("/api/admin/readings?sort=invalid")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
