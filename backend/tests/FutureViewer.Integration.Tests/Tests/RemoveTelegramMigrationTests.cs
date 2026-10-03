using FluentAssertions;
using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class RemoveTelegramMigrationTests
{
    [Fact]
    public async Task Upgrade_removes_linkage_and_revokes_only_retired_permissions()
    {
        await using var postgres = new PostgreSqlBuilder()
            .WithImage("postgres:17-alpine").Build();
        await postgres.StartAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(postgres.GetConnectionString()).Options);
        await db.GetService<IMigrator>().MigrateAsync("20260925195802_AllowRepeatedOfferAcceptance");
        var user = new User { Email = "migration@example.com", PasswordHash = "unused-test-hash" };
        var document = new LegalDocument
        {
            DocumentType = LegalDocumentType.PersonalDataConsent,
            Version = "test", ContentHash = new string('a', 64),
            PublishedAt = DateTime.UtcNow, EffectiveAt = DateTime.UtcNow, IsActive = true
        };
        var retired = new Achievement
        {
            Code = "telegram_linked", NameRu = "На связи", DescriptionRu = "Привязка Telegram",
            IconPath = "/unused.svg", Points = 10
        };
        db.Users.Add(user);
        db.LegalDocuments.Add(document);
        db.Achievements.Add(retired);
        db.UserAchievements.Add(new UserAchievement { UserId = user.Id, AchievementId = retired.Id });
        foreach (var type in new[] { ConsentType.Telegram, ConsentType.Personalization })
            db.UserConsents.Add(new UserConsent
            {
                UserId = user.Id, SubjectReference = user.PrivacySubjectId, ConsentType = type,
                LegalDocumentId = document.Id, DocumentVersion = "test", ContentHash = document.ContentHash,
                AcceptedAt = DateTime.UtcNow, CollectionSource = "migration-test"
            });
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE users SET telegram_chat_id = 12345, telegram_link_token = 'old-token' WHERE id = {user.Id}");

        await db.Database.MigrateAsync();
        db.ChangeTracker.Clear();
        (await db.Users.CountAsync()).Should().Be(1);
        (await db.Achievements.AnyAsync(x => x.Code == "telegram_linked")).Should().BeFalse();
        (await db.UserAchievements.CountAsync()).Should().Be(0);
        var consents = await db.UserConsents.ToListAsync();
        consents.Single(x => x.ConsentType == ConsentType.Telegram).RevokedAt.Should().NotBeNull();
        consents.Single(x => x.ConsentType == ConsentType.Personalization).RevokedAt.Should().BeNull();
        var columns = await db.Database.SqlQueryRaw<string>(
            "SELECT column_name AS \"Value\" FROM information_schema.columns WHERE table_name = 'users' AND column_name LIKE 'telegram_%'").ToListAsync();
        columns.Should().BeEmpty();
    }
}
