using FluentAssertions;
using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class RemoveFeedbackMigrationTests
{
    [Fact]
    public async Task Upgrade_removes_feedback_and_only_its_achievements_while_preserving_accounts_and_readings()
    {
        await using var postgres = new PostgreSqlBuilder()
            .WithImage("postgres:17-alpine").Build();
        await postgres.StartAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(postgres.GetConnectionString()).Options);
        db.Database.HasPendingModelChanges().Should().BeFalse();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261008175218_TrackIntroReadingEntitlement");

        var user = new User
        {
            Email = "retirement-migration@example.com", PasswordHash = "unused",
            HasUsedIntroReading = true, LastReadingAt = DateTime.UtcNow.Date,
            SubscriptionStatus = SubscriptionStatus.Active,
            SubscriptionExpiresAt = DateTime.UtcNow.Date.AddDays(7)
        };
        db.Users.Add(user);
        var readings = new[]
        {
            new Reading { UserId = user.Id, SpreadType = SpreadType.SingleCard, Question = "saved question", AiInterpretation = "saved answer", SavedToHistory = true },
            new Reading { UserId = user.Id, SpreadType = SpreadType.SingleCard, Question = "hidden question", SavedToHistory = true, DeletedFromHistoryAt = DateTime.UtcNow },
            new Reading { SpreadType = SpreadType.ThreeCard, Question = "guest question", SavedToHistory = false }
        };
        db.Readings.AddRange(readings);
        var retiredCodes = new[] { "first_feedback", "score_master", "perfect_10", "high_five" };
        foreach (var code in retiredCodes.Concat(["first_reading", "total_10"]))
        {
            var achievement = new Achievement
            {
                Code = code, NameRu = code, DescriptionRu = code, IconPath = "/unused.svg"
            };
            db.Achievements.Add(achievement);
            db.UserAchievements.Add(new UserAchievement { UserId = user.Id, AchievementId = achievement.Id });
        }
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO reading_feedbacks (id, reading_id, user_id, token, self_report,
                ai_score, status, created_at, scheduled_at)
            VALUES ({Guid.NewGuid()}, {readings[0].Id}, {user.Id}, 'retired-token',
                'retired answer', 10, 3, {DateTime.UtcNow}, {DateTime.UtcNow});
            """);
        db.ChangeTracker.Clear();

        await migrator.MigrateAsync();

        var tables = await db.Database.SqlQueryRaw<string>(
            "SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'public'").ToListAsync();
        tables.Should().NotContain("reading_feedbacks");
        (await db.Achievements.Select(a => a.Code).ToListAsync()).Should().BeEquivalentTo("first_reading", "total_10");
        (await db.UserAchievements.CountAsync()).Should().Be(2);
        var stored = await db.Users.SingleAsync();
        stored.Email.Should().Be(user.Email);
        stored.HasUsedIntroReading.Should().BeTrue();
        stored.LastReadingAt.Should().Be(user.LastReadingAt);
        stored.SubscriptionStatus.Should().Be(user.SubscriptionStatus);
        stored.SubscriptionExpiresAt.Should().Be(user.SubscriptionExpiresAt);
        var retained = await db.Readings.AsNoTracking().ToListAsync();
        retained.Should().HaveCount(3);
        retained.Single(r => r.Id == readings[0].Id).AiInterpretation.Should().Be("saved answer");
        retained.Single(r => r.Id == readings[1].Id).DeletedFromHistoryAt.Should().NotBeNull();
        retained.Single(r => r.Id == readings[2].Id).UserId.Should().BeNull();
    }
}
