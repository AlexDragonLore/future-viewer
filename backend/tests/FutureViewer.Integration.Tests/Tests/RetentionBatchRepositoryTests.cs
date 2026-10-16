using FluentAssertions;
using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.Infrastructure.Persistence;
using FutureViewer.Infrastructure.Persistence.Repositories;
using FutureViewer.Integration.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class RetentionBatchRepositoryTests(IntegrationTestFixture fixture) : IClassFixture<IntegrationTestFixture>
{
    [Fact]
    public async Task Cleanup_batches_delete_only_eligible_rows_and_cascade_cards_until_the_backlog_is_empty()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var repo = new PrivacyRepository(db);
        var cutoff = new DateTime(1992, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var owner = new User { Email = $"retention-batch-{Guid.NewGuid():N}@example.com", PasswordHash = "unused" };
        var card = await db.TarotCards.FirstAsync();
        var guests = Enumerable.Range(0, 5).Select(i => Reading(null, cutoff.AddSeconds(-i))).ToArray();
        var unsaved = Enumerable.Range(1, 5).Select(i => Reading(owner.Id, cutoff.AddSeconds(-i))).ToArray();
        var saved = Reading(owner.Id, cutoff.AddYears(-1));
        saved.SavedToHistory = true;
        saved.DeletedFromHistoryAt = cutoff;
        var boundaryUnsaved = Reading(owner.Id, cutoff);
        var futureGuest = Reading(null, cutoff.AddSeconds(1));
        foreach (var reading in guests)
            reading.Cards.Add(new ReadingCard { ReadingId = reading.Id, CardId = card.Id, Card = card, Position = 0, IsReversed = false });
        db.Users.Add(owner);
        db.Readings.AddRange(guests.Concat(unsaved).Concat([saved, boundaryUnsaved, futureGuest]));
        var audits = Enumerable.Range(1, 5).Select(i => Audit(cutoff.AddSeconds(-i))).ToArray();
        var boundaryAudit = Audit(cutoff);
        db.AuditEvents.AddRange(audits.Append(boundaryAudit));
        await db.SaveChangesAsync();
        foreach (var expected in new[] { 2, 2, 1, 0 })
        {
            (await repo.PurgeGuestReadingsBeforeAsync(cutoff, batchSize: 2)).Should().Be(expected);
            (await repo.PurgeUnsavedReadingsBeforeAsync(cutoff, batchSize: 2)).Should().Be(expected);
            (await repo.PurgeAuditEventsBeforeAsync(cutoff, batchSize: 2)).Should().Be(expected);
        }
        var removedIds = guests.Concat(unsaved).Select(x => x.Id).ToArray();
        (await db.Readings.AnyAsync(x => removedIds.Contains(x.Id))).Should().BeFalse();
        (await db.ReadingCards.AnyAsync(x => removedIds.Contains(x.ReadingId))).Should().BeFalse();
        foreach (var reading in new[] { saved, boundaryUnsaved, futureGuest })
            (await db.Readings.AnyAsync(x => x.Id == reading.Id)).Should().BeTrue();
        (await db.AuditEvents.AnyAsync(x => x.Id == boundaryAudit.Id)).Should().BeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Cleanup_skips_a_locked_claim_or_history_save_and_preserves_the_committed_owner(bool guest)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owner = new User { Email = $"retention-claim-{Guid.NewGuid():N}@example.com", PasswordHash = "unused" };
        var cutoff = new DateTime(1980, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var reading = Reading(guest ? null : owner.Id, cutoff.AddSeconds(-1));
        db.Users.Add(owner);
        db.Readings.Add(reading);
        await db.SaveChangesAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Readings.Where(x => x.Id == reading.Id).ExecuteUpdateAsync(update => update
            .SetProperty(x => x.UserId, owner.Id).SetProperty(x => x.SavedToHistory, true));
        await using var cleanupDb = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(db.Database.GetConnectionString()).Options);
        var repository = new PrivacyRepository(cleanupDb);
        Task<int> Purge() => guest ? repository.PurgeGuestReadingsBeforeAsync(cutoff)
            : repository.PurgeUnsavedReadingsBeforeAsync(cutoff);
        (await Purge().WaitAsync(TimeSpan.FromSeconds(5))).Should().Be(0);
        await transaction.CommitAsync();
        (await Purge()).Should().Be(0);
        var retained = await cleanupDb.Readings.AsNoTracking().SingleAsync(x => x.Id == reading.Id);
        retained.UserId.Should().Be(owner.Id);
        retained.SavedToHistory.Should().BeTrue();
    }

    private static Reading Reading(Guid? owner, DateTime at) => new()
    {
        UserId = owner, SpreadType = SpreadType.SingleCard, Question = "retention fixture",
        AiInterpretation = "retained content", CreatedAt = at
    };
    private static AuditEvent Audit(DateTime at) => new()
    {
        OccurredAt = at, EventType = "retention.fixture", CorrelationId = Guid.NewGuid().ToString("N"),
        TargetType = "test", Outcome = "success"
    };
}
