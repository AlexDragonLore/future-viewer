using FluentAssertions;
using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class IntroReadingMigrationTests
{
    [Fact]
    public async Task Upgrade_preserves_intro_usage_for_all_existing_readings_and_latest_usage_date()
    {
        await using var postgres = new PostgreSqlBuilder()
            .WithImage("postgres:17-alpine")
            .WithDatabase("intro_reading_migration_tests")
            .WithUsername("test")
            .WithPassword("test")
            .Build();
        await postgres.StartAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(postgres.GetConnectionString()).Options);
        db.Database.HasPendingModelChanges().Should().BeFalse();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261005192432_EnableHistoryByDefault");

        var newcomer = NewUser("new");
        var saved = NewUser("saved");
        var hidden = NewUser("hidden");
        var unsaved = NewUser("unsaved");
        foreach (var user in new[] { newcomer, saved, hidden, unsaved })
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO users (id, privacy_subject_id, email, password_hash, created_at)
                VALUES ({user.Id}, {user.PrivacySubjectId}, {user.Email}, {user.PasswordHash}, {user.CreatedAt});
                """);
        }
        var yesterday = DateTime.UtcNow.Date.AddDays(-1);
        var today = DateTime.UtcNow.Date;
        db.Readings.AddRange(
            new Reading { UserId = saved.Id, SpreadType = SpreadType.SingleCard, Question = "saved", CreatedAt = yesterday, SavedToHistory = true },
            new Reading { UserId = saved.Id, SpreadType = SpreadType.ThreeCard, Question = "newer", CreatedAt = today, SavedToHistory = true },
            new Reading { UserId = hidden.Id, SpreadType = SpreadType.SingleCard, Question = "hidden", CreatedAt = yesterday, SavedToHistory = true, DeletedFromHistoryAt = today },
            new Reading { UserId = unsaved.Id, SpreadType = SpreadType.SingleCard, Question = string.Empty, CreatedAt = yesterday, SavedToHistory = false });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        await migrator.MigrateAsync();

        var users = await db.Users.AsNoTracking().ToDictionaryAsync(u => u.Id);
        users[newcomer.Id].HasUsedIntroReading.Should().BeFalse();
        users[newcomer.Id].LastReadingAt.Should().BeNull();
        foreach (var id in new[] { saved.Id, hidden.Id, unsaved.Id })
            users[id].HasUsedIntroReading.Should().BeTrue();
        users[saved.Id].LastReadingAt.Should().Be(today);
        users[hidden.Id].LastReadingAt.Should().Be(yesterday);
        users[unsaved.Id].LastReadingAt.Should().Be(yesterday);
        (await db.Readings.CountAsync()).Should().Be(4);
    }

    private static User NewUser(string label) => new()
    {
        Email = $"intro-migration-{label}@example.com", PasswordHash = "unused"
    };
}
