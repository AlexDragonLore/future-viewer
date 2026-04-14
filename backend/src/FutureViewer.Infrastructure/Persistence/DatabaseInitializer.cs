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
        if (await db.TarotCards.AnyAsync(ct)) return;

        var deck = TarotDeckSeed.BuildDeck();
        await db.TarotCards.AddRangeAsync(deck, ct);
        await db.SaveChangesAsync(ct);
    }
}
