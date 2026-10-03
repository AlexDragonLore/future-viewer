using System.Text.Json;
using FluentAssertions;
using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.Interfaces;
using FutureViewer.DomainServices.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace FutureViewer.DomainServices.Tests;

public sealed class AdminAuditTests
{
    [Fact]
    public async Task DeleteFeedbackAsync_persists_content_free_audit_event()
    {
        var fixture = CreateFixture();
        var feedbackId = Guid.NewGuid();
        fixture.Feedbacks
            .Setup(x => x.DeleteAsync(feedbackId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await fixture.Sut.DeleteFeedbackAsync(
            fixture.Actor.Id,
            fixture.Actor.Email,
            feedbackId,
            CancellationToken.None);

        var audit = fixture.AuditEvents.Should().ContainSingle().Subject;
        audit.EventType.Should().Be("admin.feedback_deleted");
        audit.ActorSubjectReference.Should().Be(fixture.Actor.PrivacySubjectId);
        audit.TargetType.Should().Be("feedback");
        audit.TargetReference.Should().Be(feedbackId);
        audit.Outcome.Should().Be("success");

        var serialized = JsonSerializer.Serialize(audit);
        serialized.Should().NotContain(fixture.Actor.Email);
        serialized.Should().NotContain(fixture.Actor.Id.ToString());
        serialized.Should().NotContain("private question");
    }

    [Fact]
    public async Task CreateScheduledAsync_does_not_copy_reading_content_into_audit_or_log()
    {
        var fixture = CreateFixture();
        const string sensitiveQuestion = "private question person@example.com diagnosis";
        var reading = new Reading
        {
            UserId = fixture.Actor.Id,
            SpreadType = SpreadType.SingleCard,
            Question = sensitiveQuestion
        };
        fixture.Readings
            .Setup(x => x.GetByIdAsync(reading.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reading);
        fixture.Feedbacks
            .Setup(x => x.GetByReadingIdAsync(reading.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReadingFeedback?)null);
        fixture.Feedbacks
            .Setup(x => x.AddAsync(It.IsAny<ReadingFeedback>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReadingFeedback feedback, CancellationToken _) => feedback);

        await fixture.Sut.CreateScheduledAsync(
            fixture.Actor.Id,
            fixture.Actor.Email,
            reading.Id,
            scheduledAt: null,
            bypassDelay: false,
            replace: false,
            CancellationToken.None);

        var auditJson = JsonSerializer.Serialize(fixture.AuditEvents.Should().ContainSingle().Subject);
        auditJson.Should().NotContain(sensitiveQuestion);
        auditJson.Should().NotContain("person@example.com");
        fixture.Logs.Should().ContainSingle();
        fixture.Logs.Should().OnlyContain(message =>
            !message.Contains(sensitiveQuestion, StringComparison.Ordinal)
            && !message.Contains(fixture.Actor.Email, StringComparison.Ordinal)
            && !message.Contains(reading.Id.ToString(), StringComparison.Ordinal));
    }

    private static Fixture CreateFixture()
    {
        var feedbacks = new Mock<IFeedbackRepository>();
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
            feedbacks.Object,
            users.Object);
        var sut = new AdminService(
            feedbacks.Object,
            readings.Object,
            users.Object,
            achievements.Object,
            achievementService,
            logger,
            privacy.Object);

        return new Fixture(sut, actor, feedbacks, readings, users, auditEvents, logger.Messages);
    }

    private static User NewUser(string email) => new()
    {
        Email = email,
        PasswordHash = "not-a-real-password-hash"
    };

    private sealed record Fixture(
        AdminService Sut,
        User Actor,
        Mock<IFeedbackRepository> Feedbacks,
        Mock<IReadingRepository> Readings,
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
