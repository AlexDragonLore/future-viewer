using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FutureViewer.Infrastructure.Persistence.Repositories;

public sealed class PrivacyRepository : IPrivacyRepository
{
    private readonly AppDbContext _db;

    public PrivacyRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<LegalDocument?> GetActiveLegalDocumentAsync(
        LegalDocumentType type,
        string version,
        CancellationToken ct = default) =>
        _db.LegalDocuments.AsNoTracking().SingleOrDefaultAsync(
            x => x.DocumentType == type
                 && x.Version == version
                 && x.IsActive
                 && x.EffectiveAt <= DateTime.UtcNow,
            ct);

    public async Task AddConsentAsync(UserConsent consent, CancellationToken ct = default)
    {
        await _db.UserConsents.AddAsync(consent, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<UserConsent>> GetConsentsAsync(Guid userId, CancellationToken ct = default) =>
        await _db.UserConsents.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.AcceptedAt)
            .ToListAsync(ct);

    public Task<bool> HasActiveConsentAsync(Guid userId, ConsentType type, CancellationToken ct = default) =>
        _db.UserConsents.AnyAsync(x => x.UserId == userId && x.ConsentType == type && x.RevokedAt == null, ct);

    public async Task<bool> RevokeConsentAsync(
        Guid userId,
        ConsentType type,
        DateTime revokedAt,
        CancellationToken ct = default)
    {
        var revoked = await _db.UserConsents
            .Where(x => x.UserId == userId && x.ConsentType == type && x.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.RevokedAt, revokedAt), ct);
        return revoked > 0;
    }

    public async Task<IReadOnlyList<Reading>> GetReadingsForExportAsync(Guid userId, CancellationToken ct = default) =>
        await _db.Readings.AsNoTracking()
            .Include(x => x.Cards)
            // Export includes all retained records, including hidden history and
            // minimized quota rows. Visibility in the UI is not a data-retention filter.
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ReadingFeedback>> GetFeedbacksForExportAsync(Guid userId, CancellationToken ct = default) =>
        await _db.ReadingFeedbacks.AsNoTracking().Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<UserMemoryRule>> GetMemoryForExportAsync(Guid userId, CancellationToken ct = default) =>
        await _db.UserMemoryRules.AsNoTracking().Where(x => x.UserId == userId)
            .OrderBy(x => x.CreatedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<UserAchievement>> GetAchievementsForExportAsync(Guid userId, CancellationToken ct = default) =>
        await _db.UserAchievements.AsNoTracking().Include(x => x.Achievement)
            .Where(x => x.UserId == userId).OrderBy(x => x.UnlockedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<AnnouncementRead>> GetAnnouncementReadsForExportAsync(Guid userId, CancellationToken ct = default) =>
        await _db.AnnouncementReads.AsNoTracking().Where(x => x.UserId == userId)
            .OrderBy(x => x.ReadAt).ToListAsync(ct);

    public async Task<IReadOnlyList<ProcessedPayment>> GetPaymentsAsync(
        Guid subjectReference,
        CancellationToken ct = default) =>
        await _db.ProcessedPayments.AsNoTracking()
            .Where(x => x.SubjectReference == subjectReference)
            .OrderByDescending(x => x.ProcessedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PaymentOrder>> GetPaymentOrdersAsync(
        Guid subjectReference,
        CancellationToken ct = default) =>
        await _db.PaymentOrders.AsNoTracking()
            .Where(x => x.SubjectReference == subjectReference)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<DataSubjectRequest>> GetDataSubjectRequestsAsync(
        Guid userId,
        CancellationToken ct = default) =>
        await _db.DataSubjectRequests.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.ReceivedAt)
            .ToListAsync(ct);

    public Task<int> CountOpenRequestsDueBeforeAsync(
        DateTime dueBefore,
        CancellationToken ct = default) =>
        _db.DataSubjectRequests.CountAsync(
            x => (x.Status == DataSubjectRequestStatus.Received
                  || x.Status == DataSubjectRequestStatus.InProgress)
                 && x.DueAt <= dueBefore,
            ct);

    public async Task<bool> DeleteReadingAsync(Guid userId, Guid readingId, CancellationToken ct = default)
    {
        var deleted = await _db.Readings
            .Where(x => x.Id == readingId && x.UserId == userId && x.SavedToHistory)
            .ExecuteDeleteAsync(ct);
        return deleted > 0;
    }

    public Task<int> DeleteReadingsAsync(Guid userId, CancellationToken ct = default) =>
        _db.Readings
            .Where(x => x.UserId == userId && x.SavedToHistory)
            .ExecuteDeleteAsync(ct);

    public async Task AddDataSubjectRequestAsync(DataSubjectRequest request, CancellationToken ct = default)
    {
        await _db.DataSubjectRequests.AddAsync(request, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateDataSubjectRequestAsync(DataSubjectRequest request, CancellationToken ct = default)
    {
        if (_db.Entry(request).State == EntityState.Detached)
            _db.DataSubjectRequests.Update(request);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AddDeletionJobAsync(DataDeletionJob job, CancellationToken ct = default)
    {
        await _db.DataDeletionJobs.AddAsync(job, ct);
        await _db.SaveChangesAsync(ct);
    }

    public Task<DataDeletionJob?> GetLatestDeletionJobAsync(Guid userId, CancellationToken ct = default) =>
        _db.DataDeletionJobs.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.RequestedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<DataDeletionJob>> GetPendingDeletionJobsAsync(
        DateTime now,
        int take,
        CancellationToken ct = default) =>
        await _db.DataDeletionJobs.AsNoTracking()
            .Where(x => x.Status == DataDeletionJobStatus.Pending && x.ScheduledAt <= now)
            .OrderBy(x => x.ScheduledAt)
            .Take(take)
            .ToListAsync(ct);

    public Task<int> RecoverInterruptedDeletionJobsAsync(
        DateTime runningBefore,
        DateTime failedBefore,
        CancellationToken ct = default) =>
        _db.DataDeletionJobs
            .Where(x => (x.Status == DataDeletionJobStatus.Running
                         && (x.StartedAt == null || x.StartedAt <= runningBefore))
                        || (x.Status == DataDeletionJobStatus.Failed
                            && (x.StartedAt == null || x.StartedAt <= failedBefore)))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, DataDeletionJobStatus.Pending)
                .SetProperty(x => x.StartedAt, (DateTime?)null), ct);

    public async Task<bool> TryMarkDeletionJobRunningAsync(
        Guid id,
        DateTime startedAt,
        CancellationToken ct = default)
    {
        var updated = await _db.DataDeletionJobs
            .Where(x => x.Id == id && x.Status == DataDeletionJobStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, DataDeletionJobStatus.Running)
                .SetProperty(x => x.StartedAt, startedAt)
                .SetProperty(x => x.FailureReason, (string?)null), ct);
        return updated == 1;
    }

    public async Task CompleteAccountDeletionAsync(
        Guid jobId,
        DateTime completedAt,
        CancellationToken ct = default)
    {
        try
        {
            await CompleteAccountDeletionCoreAsync(jobId, completedAt, ct);
        }
        finally
        {
            // This batch worker reuses the scoped repository for the next job.
            // Discard tracked mutations after rollback so a later SaveChanges
            // cannot accidentally apply part of a failed deletion transaction.
            _db.ChangeTracker.Clear();
        }
    }

    private async Task CompleteAccountDeletionCoreAsync(
        Guid jobId,
        DateTime completedAt,
        CancellationToken ct)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        // Hold the row lock until commit. A recovery worker cannot reclaim a job
        // whose deletion transaction is still running, even if its lease expires.
        var job = await _db.DataDeletionJobs
            .FromSqlInterpolated($"SELECT * FROM data_deletion_jobs WHERE id = {jobId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (job is null || job.Status != DataDeletionJobStatus.Running)
            return;

        if (job.UserId is { } userId)
        {
            var user = await _db.Users.SingleOrDefaultAsync(x => x.Id == userId, ct);
            if (user is not null)
            {
                await _db.UserConsents.Where(x => x.UserId == userId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.UserId, (Guid?)null), ct);
                await _db.DataSubjectRequests.Where(x => x.UserId == userId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.UserId, (Guid?)null), ct);
                await _db.DataDeletionJobs.Where(x => x.UserId == userId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.UserId, (Guid?)null), ct);
                _db.Users.Remove(user);
            }
        }

        await _db.DataSubjectRequests
            .Where(x => x.RequestType == DataSubjectRequestType.AccountDeletion
                        && x.ResultReference == job.Id.ToString("N")
                        && x.Status == DataSubjectRequestStatus.InProgress)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, DataSubjectRequestStatus.Completed)
                .SetProperty(x => x.CompletedAt, completedAt), ct);

        job.UserId = null;
        job.Status = DataDeletionJobStatus.Completed;
        job.CompletedAt = completedAt;
        job.FailureReason = null;
        await _db.AuditEvents.AddAsync(new AuditEvent
        {
            OccurredAt = completedAt,
            EventType = "privacy.account_deleted",
            CorrelationId = job.Id.ToString("N"),
            ActorSubjectReference = job.SubjectReference,
            TargetType = "data_deletion_job",
            TargetReference = job.Id,
            Outcome = "success"
        }, ct);
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task MarkDeletionJobFailedAsync(
        Guid jobId,
        string reasonCode,
        CancellationToken ct = default)
    {
        await _db.DataDeletionJobs
            .Where(x => x.Id == jobId && x.Status == DataDeletionJobStatus.Running)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, DataDeletionJobStatus.Failed)
                .SetProperty(x => x.FailureReason, reasonCode[..Math.Min(reasonCode.Length, 64)]), ct);
    }

    public async Task AddAuditEventAsync(AuditEvent auditEvent, CancellationToken ct = default)
    {
        await _db.AuditEvents.AddAsync(auditEvent, ct);
        await _db.SaveChangesAsync(ct);
    }

    public Task<int> PurgeGuestReadingsBeforeAsync(
        DateTime cutoff,
        CancellationToken ct = default) =>
        _db.Readings
            .Where(x => x.UserId == null && x.CreatedAt <= cutoff)
            .ExecuteDeleteAsync(ct);

    public Task<int> PurgeUnsavedReadingsBeforeAsync(
        DateTime cutoff,
        CancellationToken ct = default) =>
        _db.Readings
            .Where(x => !x.SavedToHistory && x.CreatedAt < cutoff)
            .ExecuteDeleteAsync(ct);

    public Task<int> PurgeAuditEventsBeforeAsync(
        DateTime cutoff,
        CancellationToken ct = default) =>
        _db.AuditEvents
            .Where(x => x.OccurredAt < cutoff)
            .ExecuteDeleteAsync(ct);
}
