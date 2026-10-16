using FluentAssertions;
using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices;
using FutureViewer.DomainServices.Interfaces;
using FutureViewer.Infrastructure.Persistence;
using FutureViewer.Integration.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class GuestReadingRetentionTests(IntegrationTestFixture fixture) : IClassFixture<IntegrationTestFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Purge_expires_guest_content_at_thirty_days_but_preserves_newer_guests_and_claimed_history(
        bool expiredGuestSavedToHistory)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IPrivacyRepository>();
        var now = DateTime.UtcNow;
        // PostgreSQL timestamps have microsecond precision; use an exact boundary.
        now = new DateTime(now.Ticks - now.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);
        var cutoff = now - GuestReadingRetention.ContentDuration;
        var card = await db.TarotCards.FirstAsync();
        var owner = new User
        {
            Email = $"guest-retention-{Guid.NewGuid():N}@example.com",
            PasswordHash = "test-password-hash"
        };
        var olderGuest = CreateReading(card, cutoff.AddSeconds(-1), expiredGuestSavedToHistory);
        var expiredGuest = CreateReading(card, cutoff, expiredGuestSavedToHistory);
        var freshGuest = CreateReading(card, cutoff.AddSeconds(1), false);
        var accessExpiredGuest = CreateReading(card, now.AddDays(-3), false);
        var claimedReading = CreateReading(card, now.AddYears(-2), true, owner.Id);
        var hiddenReading = CreateReading(card, now.AddYears(-2), true, owner.Id);
        hiddenReading.DeletedFromHistoryAt = now.AddYears(-1);
        db.Users.Add(owner);
        db.Readings.AddRange(olderGuest, expiredGuest, freshGuest, accessExpiredGuest, claimedReading, hiddenReading);
        await db.SaveChangesAsync();

        await repository.PurgeGuestReadingsBeforeAsync(cutoff);

        foreach (var removed in new[] { olderGuest, expiredGuest })
        {
            (await db.Readings.AsNoTracking().AnyAsync(x => x.Id == removed.Id)).Should().BeFalse();
            (await db.ReadingCards.AsNoTracking().AnyAsync(x => x.ReadingId == removed.Id)).Should().BeFalse();
        }
        foreach (var retained in new[] { freshGuest, accessExpiredGuest, claimedReading, hiddenReading })
        {
            var stored = await db.Readings.AsNoTracking().SingleAsync(x => x.Id == retained.Id);
            stored.Question.Should().Be(retained.Question);
            stored.AiInterpretation.Should().Be(retained.AiInterpretation);
            (await db.ReadingCards.AsNoTracking().AnyAsync(x => x.ReadingId == retained.Id)).Should().BeTrue();
        }
    }

    [Fact]
    public async Task Generic_unsaved_cleanup_does_not_shorten_guest_retention_or_delete_saved_account_history()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IPrivacyRepository>();
        var now = DateTime.UtcNow;
        var card = await db.TarotCards.FirstAsync();
        var owner = new User
        {
            Email = $"unsaved-retention-{Guid.NewGuid():N}@example.com",
            PasswordHash = "test-password-hash"
        };
        var guest = CreateReading(card, now.AddDays(-29), false);
        var savedHistory = CreateReading(card, now.AddYears(-2), true, owner.Id);
        var hiddenHistory = CreateReading(card, now.AddYears(-2), true, owner.Id);
        hiddenHistory.DeletedFromHistoryAt = now.AddYears(-1);
        var legacyUnsaved = CreateReading(card, now.AddDays(-3), false, owner.Id);
        db.Users.Add(owner);
        db.Readings.AddRange(guest, savedHistory, hiddenHistory, legacyUnsaved);
        await db.SaveChangesAsync();

        await repository.PurgeUnsavedReadingsBeforeAsync(now.AddHours(-48));
        await repository.PurgeGuestReadingsBeforeAsync(now - GuestReadingRetention.ContentDuration);

        (await db.Readings.AsNoTracking().AnyAsync(x => x.Id == legacyUnsaved.Id)).Should().BeFalse();
        foreach (var retained in new[] { guest, savedHistory, hiddenHistory })
        {
            var stored = await db.Readings.AsNoTracking().SingleAsync(x => x.Id == retained.Id);
            stored.Question.Should().Be(retained.Question);
            stored.AiInterpretation.Should().Be(retained.AiInterpretation);
        }
    }

    private static Reading CreateReading(
        TarotCard card,
        DateTime createdAt,
        bool savedToHistory,
        Guid? userId = null)
    {
        var reading = new Reading
        {
            UserId = userId,
            SpreadType = SpreadType.SingleCard,
            Question = "На что мне стоит обратить внимание?",
            AiInterpretation = "Тестовое толкование",
            CreatedAt = createdAt,
            SavedToHistory = savedToHistory
        };
        reading.Cards.Add(new ReadingCard
        {
            ReadingId = reading.Id,
            CardId = card.Id,
            Card = card,
            Position = 1,
            IsReversed = false
        });
        return reading;
    }
}
