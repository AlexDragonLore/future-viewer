using FutureViewer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FutureViewer.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(AppDbContext db, IConfiguration? config = null, CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);
        await SeedAsync(db, config, ct);
    }

    public static async Task SeedAsync(AppDbContext db, IConfiguration? config = null, CancellationToken ct = default)
    {
        var deck = TarotDeckSeed.BuildDeck();
        var existing = await db.TarotCards.AsTracking().ToDictionaryAsync(c => c.Id, ct);

        foreach (var card in deck)
        {
            if (existing.TryGetValue(card.Id, out var tracked))
            {
                tracked.Name = card.Name;
                tracked.NameEn = card.NameEn;
                tracked.ImagePath = card.ImagePath;
                tracked.DescriptionUpright = card.DescriptionUpright;
                tracked.DescriptionReversed = card.DescriptionReversed;
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

        await SeedAchievementsAsync(db, ct);

        if (config is not null)
            await SeedAdminsAsync(db, config, ct);
    }

    public static async Task SeedAdminsAsync(AppDbContext db, IConfiguration config, CancellationToken ct = default)
    {
        var emails = config.GetSection("Admin:Emails").Get<string[]>() ?? [];
        if (emails.Length == 0) return;

        var normalized = emails
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.Trim().ToLowerInvariant())
            .Distinct()
            .ToArray();
        if (normalized.Length == 0) return;

        var users = await db.Users
            .AsTracking()
            .Where(u => normalized.Contains(u.Email) && !u.IsAdmin)
            .ToListAsync(ct);

        if (users.Count == 0) return;

        foreach (var user in users)
            user.IsAdmin = true;

        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedAchievementsAsync(AppDbContext db, CancellationToken ct)
    {
        var existing = await db.Achievements.AsTracking().ToDictionaryAsync(a => a.Code, ct);

        foreach (var item in AchievementSeed.All)
        {
            if (existing.TryGetValue(item.Code, out var tracked))
            {
                if (tracked.Points != item.Points)
                    tracked.Points = item.Points;
                continue;
            }

            await db.Achievements.AddAsync(new Achievement
            {
                Code = item.Code,
                NameRu = item.NameRu,
                DescriptionRu = item.DescriptionRu,
                IconPath = item.IconPath,
                SortOrder = item.SortOrder,
                Points = item.Points
            }, ct);
        }

        await db.SaveChangesAsync(ct);
    }

    private sealed record AchievementSeedItem(string Code, string NameRu, string DescriptionRu, string IconPath, int SortOrder, int Points);

    private static class AchievementSeed
    {
        public static readonly IReadOnlyList<AchievementSeedItem> All =
        [
            new("first_reading", "Первый расклад", "Сделайте свой первый расклад", "/achievements/first_reading.svg", 10, 10),
            new("first_feedback", "Первый отклик", "Ответьте на первый запрос обратной связи", "/achievements/first_feedback.svg", 20, 10),
            new("telegram_linked", "На связи", "Привяжите Telegram аккаунт", "/achievements/telegram_linked.svg", 30, 10),
            new("streak_3", "Три дня подряд", "Делайте расклады 3 дня подряд", "/achievements/streak_3.svg", 40, 20),
            new("streak_7", "Неделя мудрости", "Делайте расклады 7 дней подряд", "/achievements/streak_7.svg", 50, 50),
            new("streak_30", "Месяц просветления", "Делайте расклады 30 дней подряд", "/achievements/streak_30.svg", 60, 100),
            new("total_10", "Десятка", "Сделайте 10 раскладов", "/achievements/total_10.svg", 70, 20),
            new("total_50", "Полсотни", "Сделайте 50 раскладов", "/achievements/total_50.svg", 80, 50),
            new("total_100", "Сотня", "Сделайте 100 раскладов", "/achievements/total_100.svg", 90, 100),
            new("score_master", "Мастер следования", "Средний балл 8+ (минимум 10 откликов)", "/achievements/score_master.svg", 100, 100),
            new("perfect_10", "Идеальный балл", "Получите оценку 10/10", "/achievements/perfect_10.svg", 110, 20),
            new("high_five", "Пятёрка десяток", "Получите 5 раз оценку 10/10", "/achievements/high_five.svg", 120, 100)
        ];
    }
}
