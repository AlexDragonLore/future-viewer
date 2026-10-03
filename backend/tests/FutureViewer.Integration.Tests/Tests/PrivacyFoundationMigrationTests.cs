using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using FutureViewer.Domain.Entities;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Interfaces;
using FutureViewer.DomainServices.Services;
using FutureViewer.Infrastructure.Auth;
using FutureViewer.Infrastructure.Persistence;
using FutureViewer.Infrastructure.Persistence.Repositories;
using FutureViewer.Integration.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class PrivacyFoundationMigrationTests
{
    private const string PreviousProductionMigration = "20260709120000_AddReadingSoftDeleteAndAnnouncements";
    private const string PrivacyMigration = "20260731230338_AddPrivacyComplianceFoundation";

    [Fact]
    public async Task Upgrade_and_downgrade_preserve_profiles_memory_history_payments_and_issued_email_links()
    {
        await using var postgres = new PostgreSqlBuilder()
            .WithImage("postgres:17-alpine")
            .WithDatabase("privacy_migration_tests")
            .WithUsername("test")
            .WithPassword("test")
            .Build();
        await postgres.StartAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(postgres.GetConnectionString()).Options);
        db.Database.HasPendingModelChanges().Should().BeFalse();
        db.Model.FindEntityType(typeof(User))!.FindProperty("BirthDate").Should().BeNull();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousProductionMigration);

        var userId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var readingId = Guid.NewGuid();
        var memoryId = Guid.NewGuid();
        var issuedAt = DateTime.UtcNow.AddMinutes(-5);
        var expiresAt = issuedAt.AddHours(1);
        var birthDate = new DateOnly(1991, 6, 12);
        const string verificationToken = "legacy_verification_link_A-Z_0123456789abcd";
        const string resetToken = "legacy_reset_link_A-Z_0123456789abcdefghijk";
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO users (id, email, password_hash, created_at, first_name, last_name, birth_date,
                email_verification_token, email_verification_sent_at, password_reset_token, password_reset_token_expires_at)
            VALUES ({userId}, 'migration-user@example.com', 'old-password-hash', {issuedAt},
                'Synthetic', 'Profile', {birthDate}, {verificationToken}, {issuedAt}, {resetToken}, {expiresAt});
            INSERT INTO user_memory_rules (id, user_id, text, created_at, updated_at)
            VALUES ({memoryId}, {userId}, 'Synthetic existing memory', {issuedAt}, {issuedAt});
            INSERT INTO readings (id, user_id, spread_type, question, created_at, ai_interpretation)
            VALUES ({readingId}, {userId}, 0, 'Synthetic existing reading', {issuedAt}, 'Existing interpretation');
            INSERT INTO processed_payments (id, payment_id, user_id, processed_at)
            VALUES ({paymentId}, 'legacy-payment-test', {userId}, {issuedAt});
            """);

        await migrator.MigrateAsync(PrivacyMigration);
        await AssertPreservedAsync(db, birthDate, issuedAt, expiresAt, verificationToken, resetToken);
        var processedPayments = new ProcessedPaymentRepository(db);
        (await processedPayments.ExistsAsync("legacy-payment-test")).Should().BeTrue();
        (await processedPayments.ExistsAsync("unknown-legacy-payment-test")).Should().BeFalse();
        (await db.Database.SqlQueryRaw<int>("SELECT birth_year AS \"Value\" FROM users").SingleAsync())
            .Should().Be(birthDate.Year);
        (await db.Database.SqlQueryRaw<bool>("SELECT saved_to_history AS \"Value\" FROM readings").SingleAsync())
            .Should().BeTrue();
        (await db.Database.SqlQueryRaw<bool>("""
            SELECT payment.subject_reference = app_user.privacy_subject_id AS "Value"
            FROM processed_payments AS payment JOIN users AS app_user ON TRUE
            """).SingleAsync()).Should().BeTrue();

        await migrator.MigrateAsync(PreviousProductionMigration);
        await AssertPreservedAsync(db, birthDate, issuedAt, expiresAt, verificationToken, resetToken);
        (await db.Database.SqlQueryRaw<Guid>("SELECT user_id AS \"Value\" FROM processed_payments").SingleAsync())
            .Should().Be(userId);

        // Reapplying does not hash an existing hash again. Already-issued links
        // remain valid in the new application even after a schema rollback.
        await migrator.MigrateAsync(PrivacyMigration);
        await AssertPreservedAsync(db, birthDate, issuedAt, expiresAt, verificationToken, resetToken);
        await migrator.MigrateAsync();
        await AssertPreservedAsync(db, birthDate, issuedAt, expiresAt, verificationToken, resetToken);
        var auth = new AuthService(new UserRepository(db), new BCryptPasswordHasher(), new TestJwtService(),
            new CapturingEmailSender(), new FakeEmailLinkBuilder());
        (await auth.VerifyEmailAsync(verificationToken)).UserId.Should().Be(userId);
        (await auth.ResetPasswordAsync(new ResetPasswordRequest
        {
            Token = resetToken,
            NewPassword = "new-password-123"
        })).UserId.Should().Be(userId);
        var user = await db.Users.SingleAsync();
        new BCryptPasswordHasher().Verify("new-password-123", user.PasswordHash).Should().BeTrue();
        user.IsEmailVerified.Should().BeTrue();
        user.EmailVerificationToken.Should().BeNull();
        user.PasswordResetToken.Should().BeNull();
    }

    private static async Task AssertPreservedAsync(AppDbContext db, DateOnly birthDate, DateTime issuedAt,
        DateTime expiresAt, string verificationToken, string resetToken)
    {
        var row = await db.Database.SqlQueryRaw<PreservedProfile>("""
            SELECT first_name AS "FirstName", last_name AS "LastName", birth_date AS "BirthDate",
                email_verification_token AS "VerificationToken", email_verification_sent_at AS "IssuedAt",
                password_reset_token AS "ResetToken", password_reset_token_expires_at AS "ExpiresAt"
            FROM users
            """).SingleAsync();
        row.FirstName.Should().Be("Synthetic");
        row.LastName.Should().Be("Profile");
        row.BirthDate.Should().Be(birthDate);
        row.IssuedAt.Should().BeCloseTo(issuedAt, TimeSpan.FromMilliseconds(1));
        row.ExpiresAt.Should().BeCloseTo(expiresAt, TimeSpan.FromMilliseconds(1));
        row.VerificationToken.Should().Be(Hash(verificationToken));
        row.ResetToken.Should().Be(Hash(resetToken));
        (await db.Database.SqlQueryRaw<string>("SELECT text AS \"Value\" FROM user_memory_rules").SingleAsync())
            .Should().Be("Synthetic existing memory");
        (await db.Database.SqlQueryRaw<string>("SELECT ai_interpretation AS \"Value\" FROM readings").SingleAsync())
            .Should().Be("Existing interpretation");
    }

    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed class PreservedProfile
    {
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public DateOnly BirthDate { get; set; }
        public string VerificationToken { get; set; } = "";
        public DateTime IssuedAt { get; set; }
        public string ResetToken { get; set; } = "";
        public DateTime ExpiresAt { get; set; }
    }

    private sealed class TestJwtService : IJwtTokenService
    {
        public (string Token, DateTime ExpiresAt) CreateAccessToken(User user) => ("test-jwt", DateTime.UtcNow.AddHours(1));
    }
}
