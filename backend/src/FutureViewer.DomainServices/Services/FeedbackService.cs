using System.Security.Cryptography;
using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Exceptions;
using FutureViewer.DomainServices.Interfaces;

namespace FutureViewer.DomainServices.Services;

public sealed class FeedbackService
{
    public static readonly TimeSpan ScheduleDelay = TimeSpan.FromHours(24);

    private readonly IFeedbackRepository _repo;
    private readonly IReadingRepository _readings;
    private readonly IFeedbackScorer _scorer;

    public FeedbackService(
        IFeedbackRepository repo,
        IReadingRepository readings,
        IFeedbackScorer scorer)
    {
        _repo = repo;
        _readings = readings;
        _scorer = scorer;
    }

    public async Task<ReadingFeedback> ScheduleAsync(Reading reading, CancellationToken ct = default)
    {
        if (reading.UserId is null)
            throw new DomainException("Cannot schedule feedback for anonymous reading");

        var existing = await _repo.GetByReadingIdAsync(reading.Id, ct);
        if (existing is not null)
            return existing;

        var now = DateTime.UtcNow;
        var feedback = new ReadingFeedback
        {
            ReadingId = reading.Id,
            UserId = reading.UserId.Value,
            Token = GenerateToken(),
            ScheduledAt = now + ScheduleDelay,
            CreatedAt = now,
            Status = FeedbackStatus.Pending
        };

        return await _repo.AddAsync(feedback, ct);
    }

    public async Task<FeedbackDto> GetByTokenAsync(string token, CancellationToken ct = default)
    {
        var feedback = await _repo.GetByTokenAsync(token, ct)
            ?? throw new NotFoundException("Feedback not found");
        var reading = feedback.Reading
            ?? await _readings.GetByIdAsync(feedback.ReadingId, ct)
            ?? throw new NotFoundException("Associated reading not found");

        return Map(feedback, reading);
    }

    public async Task<FeedbackDto> SubmitAsync(string token, string selfReport, CancellationToken ct = default)
    {
        var feedback = await _repo.GetByTokenAsync(token, ct)
            ?? throw new NotFoundException("Feedback not found");

        if (feedback.Status == FeedbackStatus.Answered || feedback.Status == FeedbackStatus.Scored)
            throw new ConflictException("Feedback has already been submitted and cannot be changed");

        var reading = feedback.Reading
            ?? await _readings.GetByIdAsync(feedback.ReadingId, ct)
            ?? throw new NotFoundException("Associated reading not found");

        var scoring = await _scorer.ScoreAsync(
            reading.Question,
            reading.AiInterpretation ?? string.Empty,
            selfReport,
            ct);

        var finalScore = scoring.IsSincere ? Math.Clamp(scoring.Score, 1, 10) : 1;

        feedback.SelfReport = selfReport;
        feedback.AnsweredAt = DateTime.UtcNow;
        feedback.AiScore = finalScore;
        feedback.AiScoreReason = scoring.Reason;
        feedback.IsSincere = scoring.IsSincere;
        feedback.Status = FeedbackStatus.Scored;
        await _repo.UpdateAsync(feedback, ct);

        return Map(feedback, reading);
    }

    public async Task<IReadOnlyList<FeedbackDto>> GetUserFeedbacksAsync(Guid userId, CancellationToken ct = default)
    {
        var feedbacks = await _repo.GetByUserAsync(userId, take: 50, ct);
        return feedbacks
            .Where(f => f.Reading is not null)
            .Select(f => Map(f, f.Reading!))
            .ToList();
    }

    private static FeedbackDto Map(ReadingFeedback feedback, Reading reading)
    {
        return new FeedbackDto
        {
            Id = feedback.Id,
            ReadingId = feedback.ReadingId,
            Question = reading.Question,
            Interpretation = reading.AiInterpretation,
            AiScore = feedback.AiScore,
            AiScoreReason = feedback.AiScoreReason,
            IsSincere = feedback.IsSincere,
            SelfReport = feedback.SelfReport,
            Status = feedback.Status,
            CreatedAt = feedback.CreatedAt,
            AnsweredAt = feedback.AnsweredAt
        };
    }

    private static string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[24];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", string.Empty);
    }
}
