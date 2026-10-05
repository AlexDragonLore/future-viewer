using FluentAssertions;
using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class HistoryDefaultMigrationTests
{
    private const string PreviousMigration = "20261003122747_RemoveTelegramIntegration";

    [Fact]
    public async Task Upgrade_enables_implicit_defaults_and_preserves_choices_deletion_requests_and_existing_text()
    {
        await using var postgres = new PostgreSqlBuilder()
            .WithImage("postgres:17-alpine")
            .WithDatabase("history_default_migration_tests")
            .WithUsername("test")
            .WithPassword("test")
            .Build();
        await postgres.StartAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(postgres.GetConnectionString()).Options);
        db.Database.HasPendingModelChanges().Should().BeFalse();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);

        var implicitDefault = MakeUser("implicit");
        var explicitDisabled = MakeUser("disabled");
        var alreadyEnabled = MakeUser("enabled");
        alreadyEnabled.HistoryEnabled = true;
        var pendingDeletion = MakeUser("deletion-pending");
        pendingDeletion.AccountStatus = UserAccountStatus.DeletionPending;
        var blocked = MakeUser("blocked");
        blocked.AccountStatus = UserAccountStatus.Blocked;
        var requestedDeletion = MakeUser("deletion-requested");
        requestedDeletion.AccountDeletionRequestedAt = DateTime.UtcNow;
        var queuedDeletion = MakeUser("deletion-queued");
        var users = new[] { implicitDefault, explicitDisabled, alreadyEnabled, pendingDeletion, blocked, requestedDeletion, queuedDeletion };
        foreach (var user in users)
        {
            // Seed the previous schema explicitly; its default is still false.
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO users (id, privacy_subject_id, email, password_hash, created_at,
                    history_enabled, account_status, account_deletion_requested_at)
                VALUES ({user.Id}, {user.PrivacySubjectId}, {user.Email}, 'test-password-hash', {user.CreatedAt},
                    {user.HistoryEnabled}, {(int)user.AccountStatus}, {user.AccountDeletionRequestedAt});
                """);
        }

        // PrivacyService audits the privacy subject, which is distinct from the user ID.
        db.AuditEvents.Add(HistoryChoice(explicitDisabled, false));
        db.AuditEvents.Add(HistoryChoice(alreadyEnabled, true));
        db.DataDeletionJobs.Add(new DataDeletionJob
        {
            UserId = queuedDeletion.Id,
            SubjectReference = queuedDeletion.PrivacySubjectId,
            RequestedAt = DateTime.UtcNow,
            ScheduledAt = DateTime.UtcNow,
            Status = DataDeletionJobStatus.Pending
        });
        var saved = new Reading
        {
            UserId = implicitDefault.Id,
            SpreadType = SpreadType.SingleCard,
            Question = "Existing saved question",
            AiInterpretation = "Existing saved interpretation",
            SavedToHistory = true
        };
        var unsaved = new Reading
        {
            UserId = implicitDefault.Id,
            SpreadType = SpreadType.SingleCard,
            Question = string.Empty,
            SavedToHistory = false
        };
        db.Readings.AddRange(saved, unsaved);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        await migrator.MigrateAsync();

        var stored = await db.Users.AsNoTracking().ToDictionaryAsync(x => x.Id);
        stored[implicitDefault.Id].HistoryEnabled.Should().BeTrue();
        stored[alreadyEnabled.Id].HistoryEnabled.Should().BeTrue();
        foreach (var user in users.Except(new[] { implicitDefault, alreadyEnabled }))
            stored[user.Id].HistoryEnabled.Should().BeFalse(user.Email);
        (await db.AuditEvents.CountAsync()).Should().Be(2);
        var readings = await db.Readings.AsNoTracking().ToDictionaryAsync(x => x.Id);
        readings[saved.Id].Question.Should().Be(saved.Question);
        readings[saved.Id].AiInterpretation.Should().Be(saved.AiInterpretation);
        readings[saved.Id].SavedToHistory.Should().BeTrue();
        readings[unsaved.Id].Question.Should().BeEmpty();
        readings[unsaved.Id].AiInterpretation.Should().BeNull();
        readings[unsaved.Id].SavedToHistory.Should().BeFalse();

        // The database default applies to external inserts; EF still persists an explicit false.
        var databaseDefault = MakeUser("database-default");
        await InsertUsingDatabaseDefaultAsync(db, databaseDefault);
        (await db.Users.AsNoTracking().SingleAsync(x => x.Id == databaseDefault.Id)).HistoryEnabled.Should().BeTrue();
        var efOptOut = MakeUser("ef-opt-out");
        db.Users.Add(efOptOut);
        db.AuditEvents.Add(HistoryChoice(efOptOut, false));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        (await db.Users.AsNoTracking().SingleAsync(x => x.Id == efOptOut.Id)).HistoryEnabled.Should().BeFalse();

        // A rollback must not overwrite existing preferences or the text just preserved above.
        await migrator.MigrateAsync(PreviousMigration);
        (await db.Users.AsNoTracking().SingleAsync(x => x.Id == implicitDefault.Id)).HistoryEnabled.Should().BeTrue();
        (await db.Users.AsNoTracking().SingleAsync(x => x.Id == explicitDisabled.Id)).HistoryEnabled.Should().BeFalse();
        var oldDefault = MakeUser("old-default");
        await InsertUsingDatabaseDefaultAsync(db, oldDefault);
        (await db.Users.AsNoTracking().SingleAsync(x => x.Id == oldDefault.Id)).HistoryEnabled.Should().BeFalse();
        await migrator.MigrateAsync();
        (await db.Users.AsNoTracking().SingleAsync(x => x.Id == efOptOut.Id)).HistoryEnabled.Should().BeFalse();
        (await db.Readings.AsNoTracking().SingleAsync(x => x.Id == saved.Id)).AiInterpretation.Should().Be(saved.AiInterpretation);
    }

    private static User MakeUser(string name) => new()
    {
        Email = $"{name}@example.com",
        PasswordHash = "test-password-hash",
        HistoryEnabled = false
    };

    private static AuditEvent HistoryChoice(User user, bool enabled) => new()
    {
        OccurredAt = DateTime.UtcNow,
        EventType = "privacy.history_setting_changed",
        CorrelationId = Guid.NewGuid().ToString("N"),
        ActorSubjectReference = user.PrivacySubjectId,
        TargetType = "user",
        TargetReference = user.PrivacySubjectId,
        Outcome = "success",
        ReasonCode = enabled ? "enabled" : "disabled"
    };

    private static Task<int> InsertUsingDatabaseDefaultAsync(AppDbContext db, User user) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO users (id, privacy_subject_id, email, password_hash, created_at)
            VALUES ({user.Id}, {user.PrivacySubjectId}, {user.Email}, {user.PasswordHash}, {user.CreatedAt});
            """);
}
