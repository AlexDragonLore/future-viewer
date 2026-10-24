using FluentAssertions;
using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class LongReadingQuestionMigrationTests
{
    [Fact]
    public async Task Upgrade_preserves_existing_questions_and_stores_twenty_thousand_characters()
    {
        await using var postgres = new PostgreSqlBuilder()
            .WithImage("postgres:17-alpine")
            .WithDatabase("long_question_migration_tests")
            .WithUsername("test")
            .WithPassword("test")
            .Build();
        await postgres.StartAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(postgres.GetConnectionString()).Options);
        db.Database.HasPendingModelChanges().Should().BeFalse();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261018170408_AddAdminReadingViews");
        var existing = new Reading { SpreadType = SpreadType.SingleCard, Question = "Мой прежний вопрос" };
        db.Readings.Add(existing);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        await migrator.MigrateAsync();
        var longReading = new Reading { SpreadType = SpreadType.SingleCard, Question = new string('я', 20_000) };
        db.Readings.Add(longReading);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        (await db.Readings.SingleAsync(r => r.Id == existing.Id)).Question.Should().Be(existing.Question);
        (await db.Readings.SingleAsync(r => r.Id == longReading.Id)).Question.Should().Be(longReading.Question);
    }
}
