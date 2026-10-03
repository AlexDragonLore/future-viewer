namespace FutureViewer.DomainServices;

public sealed class PrivacyOptions
{
    public const string SectionName = "Privacy";

    public int DataSubjectRequestDueDays { get; init; } = 10;
    public bool ExportEnabled { get; init; } = true;
    public int DataSubjectRequestAlertDays { get; init; } = 2;
    public int DeadlineAlertPollIntervalSeconds { get; init; } = 21600;
    public int AccountDeletionGracePeriodHours { get; init; }
    public int DeletionPollIntervalSeconds { get; init; } = 60;
    public int DeletionBatchSize { get; init; } = 20;
    public int DeletionJobLeaseMinutes { get; init; } = 15;
    public int DeletionRetryDelayMinutes { get; init; } = 5;
    public int TechnicalLogRetentionDays { get; init; } = 30;
    public int AuditEventRetentionDays { get; init; } = 365;
    public int UnsavedReadingRetentionHours { get; init; } = 48;
    public int RetentionPollIntervalSeconds { get; init; } = 86400;
    public int BackupRotationDays { get; init; } = 30;
    public int LegalEvidenceRetentionDays { get; init; } = 1825;
    public string EvidenceHashKey { get; init; } = string.Empty;
}
