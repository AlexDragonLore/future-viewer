using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;

namespace FutureViewer.DomainServices.Interfaces;

public interface IPrivacyRepository
{
    Task<LegalDocument?> GetActiveLegalDocumentAsync(
        LegalDocumentType type,
        string version,
        CancellationToken ct = default);

    Task AddConsentAsync(UserConsent consent, CancellationToken ct = default);
    Task<IReadOnlyList<UserConsent>> GetConsentsAsync(Guid userId, CancellationToken ct = default);
    Task<bool> HasActiveConsentAsync(Guid userId, ConsentType type, CancellationToken ct = default);
    Task<bool> RevokeConsentAsync(Guid userId, ConsentType type, DateTime revokedAt, CancellationToken ct = default);

    Task<IReadOnlyList<Reading>> GetReadingsForExportAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<ReadingFeedback>> GetFeedbacksForExportAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<UserMemoryRule>> GetMemoryForExportAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<UserAchievement>> GetAchievementsForExportAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<AnnouncementRead>> GetAnnouncementReadsForExportAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<ProcessedPayment>> GetPaymentsAsync(Guid subjectReference, CancellationToken ct = default);
    Task<IReadOnlyList<PaymentOrder>> GetPaymentOrdersAsync(Guid subjectReference, CancellationToken ct = default);
    Task<IReadOnlyList<DataSubjectRequest>> GetDataSubjectRequestsAsync(Guid userId, CancellationToken ct = default);
    Task<int> CountOpenRequestsDueBeforeAsync(DateTime dueBefore, CancellationToken ct = default);
    Task<bool> DeleteReadingAsync(Guid userId, Guid readingId, CancellationToken ct = default);
    Task<int> DeleteReadingsAsync(Guid userId, CancellationToken ct = default);

    Task AddDataSubjectRequestAsync(DataSubjectRequest request, CancellationToken ct = default);
    Task UpdateDataSubjectRequestAsync(DataSubjectRequest request, CancellationToken ct = default);
    Task AddDeletionJobAsync(DataDeletionJob job, CancellationToken ct = default);
    Task<DataDeletionJob?> GetLatestDeletionJobAsync(Guid userId, CancellationToken ct = default);
    Task<int> RecoverInterruptedDeletionJobsAsync(DateTime runningBefore, DateTime failedBefore, CancellationToken ct = default);
    Task<IReadOnlyList<DataDeletionJob>> GetPendingDeletionJobsAsync(DateTime now, int take, CancellationToken ct = default);
    Task<bool> TryMarkDeletionJobRunningAsync(Guid id, DateTime startedAt, CancellationToken ct = default);
    Task CompleteAccountDeletionAsync(Guid jobId, DateTime completedAt, CancellationToken ct = default);
    Task MarkDeletionJobFailedAsync(Guid jobId, string reasonCode, CancellationToken ct = default);

    Task AddAuditEventAsync(AuditEvent auditEvent, CancellationToken ct = default);
    Task<int> PurgeUnsavedReadingsBeforeAsync(DateTime cutoff, CancellationToken ct = default);
    Task<int> PurgeAuditEventsBeforeAsync(DateTime cutoff, CancellationToken ct = default);
}
