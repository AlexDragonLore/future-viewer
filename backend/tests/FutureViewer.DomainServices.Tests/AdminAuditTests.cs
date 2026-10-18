using System.Text.Json;
using FluentAssertions;
using FutureViewer.Domain.Entities;
using FutureViewer.DomainServices.Interfaces;
using FutureViewer.DomainServices.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace FutureViewer.DomainServices.Tests;

public sealed class AdminAuditTests
{
    [Fact]
    public async Task DeleteUserAsync_persists_content_free_audit_event()
    {
        var fixture = CreateFixture();
        var target = NewUser("private-user@example.com");
        fixture.Users.Setup(x => x.GetByIdAsync(target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);
        fixture.Users.Setup(x => x.DeleteAsync(target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await fixture.Sut.DeleteUserAsync(fixture.Actor.Id, fixture.Actor.Email, target.Id);

        var audit = fixture.AuditEvents.Should().ContainSingle().Subject;
        audit.EventType.Should().Be("admin.user_deleted");
        audit.ActorSubjectReference.Should().Be(fixture.Actor.PrivacySubjectId);
        audit.TargetType.Should().Be("user");
        audit.TargetReference.Should().Be(target.PrivacySubjectId);
        audit.Outcome.Should().Be("success");

        var serialized = JsonSerializer.Serialize(audit);
        serialized.Should().NotContain(fixture.Actor.Email).And.NotContain(target.Email);
        serialized.Should().NotContain(fixture.Actor.Id.ToString()).And.NotContain(target.Id.ToString());
    }

    [Fact]
    public async Task SetAdminAsync_does_not_copy_account_details_into_audit_or_log()
    {
        var fixture = CreateFixture();
        var target = NewUser("private-user@example.com");
        fixture.Users.Setup(x => x.GetByIdAsync(target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);

        await fixture.Sut.SetAdminAsync(fixture.Actor.Id, fixture.Actor.Email, target.Id, true);

        var audit = fixture.AuditEvents.Should().ContainSingle().Subject;
        audit.EventType.Should().Be("admin.user_role_changed");
        audit.ReasonCode.Should().Be("admin_enabled");
        JsonSerializer.Serialize(audit).Should().NotContain(target.Email).And.NotContain(fixture.Actor.Email);
        fixture.Logs.Should().ContainSingle();
        fixture.Logs.Should().OnlyContain(message =>
            !message.Contains(target.Email, StringComparison.Ordinal)
            && !message.Contains(fixture.Actor.Email, StringComparison.Ordinal)
            && !message.Contains(target.Id.ToString(), StringComparison.Ordinal));
    }

    private static Fixture CreateFixture()
    {
        var readings = new Mock<IReadingRepository>();
        var users = new Mock<IUserRepository>();
        var achievements = new Mock<IAchievementRepository>();
        var privacy = new Mock<IPrivacyRepository>();
        var logger = new CaptureLogger<AdminService>();
        var auditEvents = new List<AuditEvent>();
        var actor = NewUser("admin+private@example.com");

        users
            .Setup(x => x.GetByIdAsync(actor.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(actor);
        privacy
            .Setup(x => x.AddAuditEventAsync(It.IsAny<AuditEvent>(), It.IsAny<CancellationToken>()))
            .Callback((AuditEvent audit, CancellationToken _) => auditEvents.Add(audit))
            .Returns(Task.CompletedTask);

        var achievementService = new AchievementService(
            achievements.Object,
            readings.Object,
            users.Object);
        var sut = new AdminService(
            readings.Object,
            users.Object,
            achievements.Object,
            achievementService,
            logger,
            privacy.Object);

        return new Fixture(sut, actor, users, auditEvents, logger.Messages);
    }

    private static User NewUser(string email) => new()
    {
        Email = email,
        PasswordHash = "not-a-real-password-hash"
    };

    private sealed record Fixture(
        AdminService Sut,
        User Actor,
        Mock<IUserRepository> Users,
        List<AuditEvent> AuditEvents,
        List<string> Logs);

    private sealed class CaptureLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
            if (exception is not null)
                Messages.Add(exception.ToString());
        }
    }
}
