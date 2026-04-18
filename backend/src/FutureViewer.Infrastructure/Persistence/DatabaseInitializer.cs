using Microsoft.EntityFrameworkCore;

namespace FutureViewer.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(AppDbContext db, CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);
        await SeedAsync(db, ct);
    }

    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        var deck = TarotDeckSeed.BuildDeck();
        var existing = await db.TarotCards.AsTracking().ToDictionaryAsync(c => c.Id, ct);

        foreach (var card in deck)
        {
            if (existing.TryGetValue(card.Id, out var tracked))
            {
                tracked.NameEn = card.NameEn;
                tracked.ShortUpright = card.ShortUpright;
                tracked.ShortReversed = card.ShortReversed;
                tracked.UprightKeywords = card.UprightKeywords;
                tracked.ReversedKeywords = card.ReversedKeywords;
                tracked.SuggestedTone = card.SuggestedTone;
                tracked.Aliases = card.Aliases;
            }
            else
            {
                await db.TarotCards.AddAsync(card, ct);
            }
        }

        await db.SaveChangesAsync(ct);

        if (!await db.DeckVariants.AnyAsync(ct))
        {
            var variants = TarotDeckSeed.BuildDeckVariants(deck);
            await db.DeckVariants.AddRangeAsync(variants, ct);
            await db.SaveChangesAsync(ct);
        }
    }
}
