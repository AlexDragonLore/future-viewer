using System.Security.Cryptography;
using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.DTOs.Admin;
using FutureViewer.DomainServices.Exceptions;
using FutureViewer.DomainServices.Interfaces;
using Microsoft.Extensions.Logging;

namespace FutureViewer.DomainServices.Services;

public sealed class AdminService
{
    private readonly IFeedbackRepository _feedbacks;
    private readonly IReadingRepository _readings;
    private readonly IUserRepository _users;
    private readonly AchievementService _achievements;
    private readonly ILogger<AdminService> _logger;

    public AdminService(
        IFeedbackRepository feedbacks,
        IReadingRepository readings,
        IUserRepository users,
        AchievementService achievements,
        ILogger<AdminService> logger)
    {
        _feedbacks = feedbacks;
        _readings = readings;
        _users = users;
        _achievements = achievements;
        _logger = logger;
    }

    public async Task<AdminFeedbackListResult> SearchFeedbacksAsync(
        Guid? userId,
        FeedbackStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var safePage = Math.Max(1, page);
        var safeSize = Math.Clamp(pageSize, 1, 100);
        var skip = (safePage - 1) * safeSize;

        var items = await _feedbacks.SearchAsync(userId, status, skip, safeSize, ct);
        var total = await _feedbacks.CountAsync(userId, status, ct);

        return new AdminFeedbackListResult
        {
            Items = items.Select(MapAdmin).ToList(),
            Total = total
        };
    }

    public async Task<AdminFeedbackDto> CreateScheduledAsync(
        Guid actorId,
        string actorEmail,
        Guid readingId,
        DateTime? scheduledAt,
        bool bypassDelay,
        bool replace,
        CancellationToken ct = default)
    {
        var reading = await _readings.GetByIdAsync(readingId, ct)
            ?? throw new NotFoundException("Reading not found");
        if (reading.UserId is null)
            throw new DomainException("Cannot create feedback for anonymous reading");

        var existing = await _feedbacks.GetByReadingIdAsync(readingId, ct);
        if (existing is not null)
        {
            if (!replace)
                throw new ConflictException("Feedback already exists for this reading. Pass replace=true to overwrite.");
            await _feedbacks.DeleteAsync(existing.Id, ct);
        }

        var now = DateTime.UtcNow;
        var effectiveScheduledAt = bypassDelay
            ? now.AddMinutes(-1)
            : (scheduledAt ?? now + FeedbackService.ScheduleDelay);

        var feedback = new ReadingFeedback
        {
            ReadingId = reading.Id,
            UserId = reading.UserId.Value,
            Token = GenerateToken(),
            ScheduledAt = effectiveScheduledAt,
            CreatedAt = now,
            Status = FeedbackStatus.Pending
        };

        var saved = await _feedbacks.AddAsync(feedback, ct);

        _logger.LogInformation(
            "Admin {ActorEmail} ({ActorId}) created feedback {FeedbackId} for reading {ReadingId} (bypassDelay={Bypass}, replace={Replace})",
            actorEmail, actorId, saved.Id, readingId, bypassDelay, replace);

        return MapAdmin(saved, reading, await _users.GetByIdAsync(reading.UserId.Value, ct));
    }

    public async Task<AdminFeedbackDto> CreateSyntheticAsync(
        Guid actorId,
        string actorEmail,
        Guid readingId,
        int aiScore,
        string? aiScoreReason,
        bool isSincere,
        string? selfReport,
        CancellationToken ct = default)
    {
        if (aiScore is < 1 or > 10)
            throw new DomainException("aiScore must be between 1 and 10");

        var reading = await _readings.GetByIdAsync(readingId, ct)
            ?? throw new NotFoundException("Reading not found");
        if (reading.UserId is null)
            throw new DomainException("Cannot create synthetic feedback for anonymous reading");

        var existing = await _feedbacks.GetByReadingIdAsync(readingId, ct);
        if (existing is not null)
            throw new ConflictException("Feedback already exists for this reading. Edit or delete the existing feedback instead.");

        var now = DateTime.UtcNow;
        var feedback = new ReadingFeedback
        {
            ReadingId = reading.Id,
            UserId = reading.UserId.Value,
            Token = GenerateToken(),
            ScheduledAt = now,
            CreatedAt = now,
            NotifiedAt = now,
            AnsweredAt = now,
            Status = FeedbackStatus.Scored,
            AiScore = aiScore,
            AiScoreReason = aiScoreReason,
            IsSincere = isSincere,
            SelfReport = selfReport
        };

        var saved = await _feedbacks.AddAsync(feedback, ct);

        _logger.LogInformation(
            "Admin {ActorEmail} ({ActorId}) created synthetic feedback {FeedbackId} for reading {ReadingId} score={Score}",
            actorEmail, actorId, saved.Id, readingId, aiScore);

        await _achievements.CheckAndGrantAsync(saved.UserId, ct);

        return MapAdmin(saved, reading, await _users.GetByIdAsync(reading.UserId.Value, ct));
    }

    public async Task<AdminFeedbackDto> UpdateAsync(
        Guid actorId,
        string actorEmail,
        Guid feedbackId,
        AdminFeedbackUpdate update,
        CancellationToken ct = default)
    {
        var feedback = await _feedbacks.GetByIdAsync(feedbackId, ct)
            ?? throw new NotFoundException("Feedback not found");

        if (update.AiScore is { } score)
        {
            if (score is < 1 or > 10)
                throw new DomainException("aiScore must be between 1 and 10");
            feedback.AiScore = score;
        }
        if (update.AiScoreReason is not null) feedback.AiScoreReason = update.AiScoreReason;
        if (update.IsSincere.HasValue) feedback.IsSincere = update.IsSincere;
        if (update.Status.HasValue) feedback.Status = update.Status.Value;
        if (update.SelfReport is not null) feedback.SelfReport = update.SelfReport;
        if (update.ScheduledAt.HasValue) feedback.ScheduledAt = update.ScheduledAt.Value;
        if (update.NotifiedAt.HasValue) feedback.NotifiedAt = update.NotifiedAt;
        if (update.AnsweredAt.HasValue) feedback.AnsweredAt = update.AnsweredAt;

        await _feedbacks.UpdateAsync(feedback, ct);

        _logger.LogInformation(
            "Admin {ActorEmail} ({ActorId}) updated feedback {FeedbackId}: score={Score} status={Status}",
            actorEmail, actorId, feedbackId, feedback.AiScore, feedback.Status);

        await _achievements.CheckAndGrantAsync(feedback.UserId, ct);

        var reading = feedback.Reading ?? await _readings.GetByIdAsync(feedback.ReadingId, ct);
        return MapAdmin(feedback, reading, await _users.GetByIdAsync(feedback.UserId, ct));
    }

    public async Task DeleteFeedbackAsync(
        Guid actorId,
        string actorEmail,
        Guid feedbackId,
        CancellationToken ct = default)
    {
        var existed = await _feedbacks.DeleteAsync(feedbackId, ct);
        if (!existed)
            throw new NotFoundException("Feedback not found");

        _logger.LogInformation(
            "Admin {ActorEmail} ({ActorId}) deleted feedback {FeedbackId}",
            actorEmail, actorId, feedbackId);
    }

    private static AdminFeedbackDto MapAdmin(ReadingFeedback feedback) =>
        MapAdmin(feedback, feedback.Reading, feedback.User);

    private static AdminFeedbackDto MapAdmin(ReadingFeedback feedback, Reading? reading, User? user) =>
        new()
        {
            Id = feedback.Id,
            ReadingId = feedback.ReadingId,
            UserId = feedback.UserId,
            UserEmail = user?.Email,
            Question = reading?.Question,
            Token = feedback.Token,
            SelfReport = feedback.SelfReport,
            AiScore = feedback.AiScore,
            AiScoreReason = feedback.AiScoreReason,
            IsSincere = feedback.IsSincere,
            ScheduledAt = feedback.ScheduledAt,
            NotifiedAt = feedback.NotifiedAt,
            AnsweredAt = feedback.AnsweredAt,
            Status = feedback.Status,
            CreatedAt = feedback.CreatedAt
        };

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

public sealed class AdminFeedbackListResult
{
    public required IReadOnlyList<AdminFeedbackDto> Items { get; init; }
    public required int Total { get; init; }
}

public sealed class AdminFeedbackUpdate
{
    public int? AiScore { get; init; }
    public string? AiScoreReason { get; init; }
    public bool? IsSincere { get; init; }
    public FeedbackStatus? Status { get; init; }
    public string? SelfReport { get; init; }
    public DateTime? ScheduledAt { get; init; }
    public DateTime? NotifiedAt { get; init; }
    public DateTime? AnsweredAt { get; init; }
}
