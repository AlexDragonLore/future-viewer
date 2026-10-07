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
    public async Task Purge_removes_expired_guests_and_their_cards_but_retains_fresh_guests_and_claimed_readings(
        bool expiredGuestSavedToHistory)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IPrivacyRepository>();
        var cutoff = DateTime.UtcNow - GuestReadingRetention.Duration;
        var card = await db.TarotCards.FirstAsync();
        var owner = new User
        {
            Email = $"guest-retention-{Guid.NewGuid():N}@example.com",
            PasswordHash = "test-password-hash"
        };
        var expiredGuest = CreateReading(card, cutoff, expiredGuestSavedToHistory);
        var freshGuest = CreateReading(card, cutoff.AddHours(1), false);
        var claimedReading = CreateReading(card, cutoff.AddHours(-1), true, owner.Id);
        db.Users.Add(owner);
        db.Readings.AddRange(expiredGuest, freshGuest, claimedReading);
        await db.SaveChangesAsync();

        (await repository.PurgeGuestReadingsBeforeAsync(cutoff)).Should().Be(1);

        (await db.Readings.AsNoTracking().AnyAsync(x => x.Id == expiredGuest.Id)).Should().BeFalse();
        (await db.ReadingCards.AsNoTracking().AnyAsync(x => x.ReadingId == expiredGuest.Id)).Should().BeFalse();
        (await db.Readings.AsNoTracking().AnyAsync(x => x.Id == freshGuest.Id)).Should().BeTrue();
        (await db.ReadingCards.AsNoTracking().AnyAsync(x => x.ReadingId == freshGuest.Id)).Should().BeTrue();
        (await db.Readings.AsNoTracking().AnyAsync(x => x.Id == claimedReading.Id)).Should().BeTrue();
        (await db.ReadingCards.AsNoTracking().AnyAsync(x => x.ReadingId == claimedReading.Id)).Should().BeTrue();
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
