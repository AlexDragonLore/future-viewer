using FluentAssertions;
using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class AdminReadingViewMigrationTests
{
    [Fact]
    public async Task Migration_preserves_content_and_cascades_receipts_on_account_deletion()
    {
        await using var postgres = new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build();
        await postgres.StartAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(postgres.GetConnectionString()).Options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261018073538_RemoveRetiredFeedback");
        var admin = new User { Email = "review-migration@example.com", PasswordHash = "unused", IsAdmin = true };
        var reading = new Reading { SpreadType = SpreadType.SingleCard, Question = "Preserved question" };
        db.Users.Add(admin);
        db.Readings.Add(reading);
        await db.SaveChangesAsync();
        await migrator.MigrateAsync();
        db.Database.HasPendingModelChanges().Should().BeFalse();
        db.AdminReadingViews.Add(new AdminReadingView { AdminId = admin.Id, ReadingId = reading.Id });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        await db.Users.Where(u => u.Id == admin.Id).ExecuteDeleteAsync();
        (await db.AdminReadingViews.CountAsync()).Should().Be(0);
        await migrator.MigrateAsync("20261018073538_RemoveRetiredFeedback");
        (await db.Readings.SingleAsync()).Question.Should().Be("Preserved question");
        await migrator.MigrateAsync();
        (await db.AdminReadingViews.CountAsync()).Should().Be(0);
    }
}
