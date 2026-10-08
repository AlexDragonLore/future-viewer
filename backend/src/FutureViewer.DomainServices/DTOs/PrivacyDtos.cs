namespace FutureViewer.DomainServices.DTOs;

public sealed class PrivacySettingsDto
{
    public bool HistoryEnabled { get; init; }
    public bool AgeConfirmed18 { get; init; }
    public bool PersonalizationEnabled { get; init; }
    public bool MarketingEnabled { get; init; }
    public bool AnalyticsEnabled { get; init; }
}

public sealed class UpdateHistorySettingRequest
{
    public bool Enabled { get; init; }
}

public sealed class ReauthenticationRequest
{
    public string Password { get; init; } = string.Empty;
}

public sealed class ConsentDto
{
    public required Guid Id { get; init; }
    public required string ConsentType { get; init; }
    public required string DocumentVersion { get; init; }
    public required string ContentHash { get; init; }
    public required DateTime AcceptedAt { get; init; }
    public DateTime? RevokedAt { get; init; }
    public required string CollectionSource { get; init; }
}

public sealed class PrivacyExportDto
{
    public required DateTime GeneratedAt { get; init; }
    public required PrivacyProfileExportDto Profile { get; init; }
    public required IReadOnlyList<PrivacyReadingExportDto> Readings { get; init; }
    public required IReadOnlyList<PrivacyFeedbackExportDto> Feedbacks { get; init; }
    public required IReadOnlyList<UserMemoryRuleDto> MemoryRules { get; init; }
    public required IReadOnlyList<PrivacyAchievementExportDto> Achievements { get; init; }
    public required IReadOnlyList<PrivacyAnnouncementReadExportDto> AnnouncementReads { get; init; }
    public required IReadOnlyList<ConsentDto> Consents { get; init; }
    public required IReadOnlyList<PrivacyPaymentExportDto> Payments { get; init; }
    public required IReadOnlyList<PrivacyRequestExportDto> Requests { get; init; }
}

public sealed class PrivacyProfileExportDto
{
    public required Guid Id { get; init; }
    public required string Email { get; init; }
    public required DateTime CreatedAt { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public int? BirthYear { get; init; }
    public bool AgeConfirmed18 { get; init; }
    public bool HistoryEnabled { get; init; }
    public required string AccountStatus { get; init; }
    public required string SubscriptionStatus { get; init; }
    public DateTime? SubscriptionExpiresAt { get; init; }
    public bool HasUsedIntroReading { get; init; }
    public DateTime? LastReadingAt { get; init; }
    public bool IsEmailVerified { get; init; }
    public bool IsAdmin { get; init; }
}

public sealed class PrivacyReadingExportDto
{
    public required Guid Id { get; init; }
    public required string SpreadType { get; init; }
    public required string DeckType { get; init; }
    public required string Question { get; init; }
    public required DateTime CreatedAt { get; init; }
    public bool SavedToHistory { get; init; }
    public DateTime? DeletedFromHistoryAt { get; init; }
    public string? AiInterpretation { get; init; }
    public string? AiModel { get; init; }
    public required IReadOnlyList<PrivacyReadingCardExportDto> Cards { get; init; }
}

public sealed class PrivacyReadingCardExportDto
{
    public required int CardId { get; init; }
    public required int Position { get; init; }
    public required bool IsReversed { get; init; }
}

public sealed class PrivacyFeedbackExportDto
{
    public required Guid Id { get; init; }
    public required Guid ReadingId { get; init; }
    public string? SelfReport { get; init; }
    public int? AiScore { get; init; }
    public string? AiScoreReason { get; init; }
    public bool? IsSincere { get; init; }
    public required string Status { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime ScheduledAt { get; init; }
    public DateTime? NotifiedAt { get; init; }
    public DateTime? AnsweredAt { get; init; }
}

public sealed class PrivacyAchievementExportDto
{
    public required Guid AchievementId { get; init; }
    public string? Code { get; init; }
    public required DateTime UnlockedAt { get; init; }
}

public sealed class PrivacyAnnouncementReadExportDto
{
    public required Guid AnnouncementId { get; init; }
    public required DateTime ReadAt { get; init; }
}

public sealed class PrivacyPaymentExportDto
{
    public required string OrderId { get; init; }
    public string? ProviderPaymentId { get; init; }
    public string? Provider { get; init; }
    public string? TariffCode { get; init; }
    public decimal? Amount { get; init; }
    public string? Currency { get; init; }
    public int? AccessDays { get; init; }
    public required string Status { get; init; }
    public string? ReceiptStatus { get; init; }
    public required DateTime CreatedAt { get; init; }
}


public sealed class PrivacyRequestExportDto
{
    public required Guid Id { get; init; }
    public required string RequestType { get; init; }
    public required DateTime ReceivedAt { get; init; }
    public DateTime? IdentityVerifiedAt { get; init; }
    public required DateTime DueAt { get; init; }
    public required string Status { get; init; }
    public DateTime? CompletedAt { get; init; }
}

public sealed class AccountDeletionStatusDto
{
    public bool Requested { get; init; }
    public string? Status { get; init; }
    public DateTime? RequestedAt { get; init; }
    public DateTime? ScheduledAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
}
