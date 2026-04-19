using FluentAssertions;
using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Exceptions;
using FutureViewer.DomainServices.Interfaces;
using FutureViewer.DomainServices.Services;
using Moq;

namespace FutureViewer.DomainServices.Tests;

public sealed class FeedbackServiceTests
{
    private static Reading NewReading(Guid? userId = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId ?? Guid.NewGuid(),
            SpreadType = SpreadType.SingleCard,
            Question = "Что меня ждёт?",
            AiInterpretation = "интерпретация"
        };

    private static FeedbackService BuildService(
        Mock<IFeedbackRepository>? repo = null,
        Mock<IReadingRepository>? readings = null,
        Mock<IFeedbackScorer>? scorer = null)
    {
        return new FeedbackService(
            (repo ?? new Mock<IFeedbackRepository>()).Object,
            (readings ?? new Mock<IReadingRepository>()).Object,
            (scorer ?? new Mock<IFeedbackScorer>()).Object);
    }

    [Fact]
    public async Task ScheduleAsync_creates_feedback_with_24h_delay_and_pending_status()
    {
        var repo = new Mock<IFeedbackRepository>();
        repo.Setup(r => r.GetByReadingIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReadingFeedback?)null);
        ReadingFeedback? saved = null;
        repo.Setup(r => r.AddAsync(It.IsAny<ReadingFeedback>(), It.IsAny<CancellationToken>()))
            .Callback((ReadingFeedback f, CancellationToken _) => saved = f)
            .ReturnsAsync((ReadingFeedback f, CancellationToken _) => f);

        var reading = NewReading();
        var sut = BuildService(repo: repo);

        var before = DateTime.UtcNow;
        var result = await sut.ScheduleAsync(reading);

        result.Should().NotBeNull();
        saved.Should().NotBeNull();
        saved!.ReadingId.Should().Be(reading.Id);
        saved.UserId.Should().Be(reading.UserId!.Value);
        saved.Status.Should().Be(FeedbackStatus.Pending);
        saved.Token.Should().NotBeNullOrEmpty();
        (saved.ScheduledAt - saved.CreatedAt).Should().BeCloseTo(FeedbackService.ScheduleDelay, TimeSpan.FromSeconds(1));
        saved.ScheduledAt.Should().BeOnOrAfter(before + FeedbackService.ScheduleDelay - TimeSpan.FromSeconds(2));
        repo.Verify(r => r.AddAsync(It.IsAny<ReadingFeedback>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ScheduleAsync_throws_for_anonymous_reading()
    {
        var sut = BuildService();
        var reading = new Reading
        {
            Id = Guid.NewGuid(),
            UserId = null,
            SpreadType = SpreadType.SingleCard,
            Question = "q"
        };

        await FluentActions.Awaiting(() => sut.ScheduleAsync(reading))
            .Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task ScheduleAsync_is_idempotent_returns_existing_and_does_not_add_again()
    {
        var reading = NewReading();
        var existing = new ReadingFeedback
        {
            ReadingId = reading.Id,
            UserId = reading.UserId!.Value,
            Token = "existing-token",
            ScheduledAt = DateTime.UtcNow.AddHours(20),
            Status = FeedbackStatus.Pending
        };

        var repo = new Mock<IFeedbackRepository>();
        repo.Setup(r => r.GetByReadingIdAsync(reading.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var sut = BuildService(repo: repo);

        var result = await sut.ScheduleAsync(reading);

        result.Should().BeSameAs(existing);
        repo.Verify(r => r.AddAsync(It.IsAny<ReadingFeedback>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetByTokenAsync_returns_dto_with_reading_data()
    {
        var reading = NewReading();
        var feedback = new ReadingFeedback
        {
            ReadingId = reading.Id,
            UserId = reading.UserId!.Value,
            Token = "tok",
            ScheduledAt = DateTime.UtcNow,
            Reading = reading,
            Status = FeedbackStatus.Pending
        };

        var repo = new Mock<IFeedbackRepository>();
        repo.Setup(r => r.GetByTokenAsync("tok", It.IsAny<CancellationToken>()))
            .ReturnsAsync(feedback);

        var sut = BuildService(repo: repo);

        var dto = await sut.GetByTokenAsync("tok");

        dto.ReadingId.Should().Be(reading.Id);
        dto.Question.Should().Be(reading.Question);
        dto.Interpretation.Should().Be(reading.AiInterpretation);
        dto.Status.Should().Be(FeedbackStatus.Pending);
    }

    [Fact]
    public async Task GetByTokenAsync_throws_not_found_when_token_missing()
    {
        var repo = new Mock<IFeedbackRepository>();
        repo.Setup(r => r.GetByTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReadingFeedback?)null);

        var sut = BuildService(repo: repo);

        await FluentActions.Awaiting(() => sut.GetByTokenAsync("missing"))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task SubmitAsync_scores_feedback_and_marks_scored()
    {
        var reading = NewReading();
        var feedback = new ReadingFeedback
        {
            ReadingId = reading.Id,
            UserId = reading.UserId!.Value,
            Token = "tok",
            ScheduledAt = DateTime.UtcNow,
            Reading = reading,
            Status = FeedbackStatus.Notified
        };

        var repo = new Mock<IFeedbackRepository>();
        repo.Setup(r => r.GetByTokenAsync("tok", It.IsAny<CancellationToken>()))
            .ReturnsAsync(feedback);
        repo.Setup(r => r.UpdateAsync(It.IsAny<ReadingFeedback>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var scorer = new Mock<IFeedbackScorer>();
        scorer.Setup(s => s.ScoreAsync(reading.Question, reading.AiInterpretation!, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FeedbackScoringResult { Score = 8, Reason = "хорошо", IsSincere = true });

        var sut = BuildService(repo: repo, scorer: scorer);

        var dto = await sut.SubmitAsync("tok", "I tried the advice all week and reflected nightly.");

        dto.AiScore.Should().Be(8);
        dto.IsSincere.Should().BeTrue();
        dto.AiScoreReason.Should().Be("хорошо");
        dto.Status.Should().Be(FeedbackStatus.Scored);
        feedback.Status.Should().Be(FeedbackStatus.Scored);
        feedback.AnsweredAt.Should().NotBeNull();
        repo.Verify(r => r.UpdateAsync(It.IsAny<ReadingFeedback>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task SubmitAsync_insincere_response_is_floored_to_one()
    {
        var reading = NewReading();
        var feedback = new ReadingFeedback
        {
            ReadingId = reading.Id,
            UserId = reading.UserId!.Value,
            Token = "tok",
            ScheduledAt = DateTime.UtcNow,
            Reading = reading,
            Status = FeedbackStatus.Pending
        };

        var repo = new Mock<IFeedbackRepository>();
        repo.Setup(r => r.GetByTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(feedback);

        var scorer = new Mock<IFeedbackScorer>();
        scorer.Setup(s => s.ScoreAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FeedbackScoringResult { Score = 7, Reason = "подозрительно", IsSincere = false });

        var sut = BuildService(repo: repo, scorer: scorer);

        var dto = await sut.SubmitAsync("tok", "some answer of sufficient length");

        dto.AiScore.Should().Be(1);
        dto.IsSincere.Should().BeFalse();
    }

    [Fact]
    public async Task SubmitAsync_clamps_scores_outside_range()
    {
        var reading = NewReading();
        var feedback = new ReadingFeedback
        {
            ReadingId = reading.Id,
            UserId = reading.UserId!.Value,
            Token = "tok",
            ScheduledAt = DateTime.UtcNow,
            Reading = reading,
            Status = FeedbackStatus.Pending
        };

        var repo = new Mock<IFeedbackRepository>();
        repo.Setup(r => r.GetByTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(feedback);

        var scorer = new Mock<IFeedbackScorer>();
        scorer.Setup(s => s.ScoreAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FeedbackScoringResult { Score = 42, Reason = "overshoot", IsSincere = true });

        var sut = BuildService(repo: repo, scorer: scorer);

        var dto = await sut.SubmitAsync("tok", "valid answer");

        dto.AiScore.Should().Be(10);
    }

    [Fact]
    public async Task SubmitAsync_throws_when_already_scored()
    {
        var reading = NewReading();
        var feedback = new ReadingFeedback
        {
            ReadingId = reading.Id,
            UserId = reading.UserId!.Value,
            Token = "tok",
            ScheduledAt = DateTime.UtcNow,
            Reading = reading,
            Status = FeedbackStatus.Scored
        };

        var repo = new Mock<IFeedbackRepository>();
        repo.Setup(r => r.GetByTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(feedback);

        var sut = BuildService(repo: repo);

        await FluentActions.Awaiting(() => sut.SubmitAsync("tok", "valid answer"))
            .Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task SubmitAsync_throws_when_already_answered()
    {
        var reading = NewReading();
        var feedback = new ReadingFeedback
        {
            ReadingId = reading.Id,
            UserId = reading.UserId!.Value,
            Token = "tok",
            ScheduledAt = DateTime.UtcNow,
            Reading = reading,
            Status = FeedbackStatus.Answered
        };

        var repo = new Mock<IFeedbackRepository>();
        repo.Setup(r => r.GetByTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(feedback);

        var sut = BuildService(repo: repo);

        await FluentActions.Awaiting(() => sut.SubmitAsync("tok", "valid answer"))
            .Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task SubmitAsync_throws_when_expired()
    {
        var reading = NewReading();
        var feedback = new ReadingFeedback
        {
            ReadingId = reading.Id,
            UserId = reading.UserId!.Value,
            Token = "tok",
            ScheduledAt = DateTime.UtcNow,
            Reading = reading,
            Status = FeedbackStatus.Expired
        };

        var repo = new Mock<IFeedbackRepository>();
        repo.Setup(r => r.GetByTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(feedback);

        var sut = BuildService(repo: repo);

        await FluentActions.Awaiting(() => sut.SubmitAsync("tok", "valid answer"))
            .Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task GetUserFeedbacksAsync_maps_and_skips_missing_readings()
    {
        var userId = Guid.NewGuid();
        var withReading = NewReading(userId);
        var f1 = new ReadingFeedback
        {
            ReadingId = withReading.Id,
            UserId = userId,
            Token = "t1",
            ScheduledAt = DateTime.UtcNow,
            Reading = withReading,
            Status = FeedbackStatus.Scored
        };
        var orphanReadingId = Guid.NewGuid();
        var f2 = new ReadingFeedback
        {
            ReadingId = orphanReadingId,
            UserId = userId,
            Token = "t2",
            ScheduledAt = DateTime.UtcNow,
            Status = FeedbackStatus.Pending
        };

        var repo = new Mock<IFeedbackRepository>();
        repo.Setup(r => r.GetByUserAsync(userId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ReadingFeedback> { f1, f2 });

        var readings = new Mock<IReadingRepository>();
        readings.Setup(r => r.GetByIdAsync(orphanReadingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Reading?)null);

        var sut = BuildService(repo: repo, readings: readings);

        var list = await sut.GetUserFeedbacksAsync(userId);

        list.Should().HaveCount(1);
        list[0].ReadingId.Should().Be(withReading.Id);
    }
}
