using System.Security.Cryptography;
using System.Text;
using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Exceptions;
using FutureViewer.DomainServices.Interfaces;

namespace FutureViewer.DomainServices.Services;

public sealed class PrivacyService
{
    private static readonly HashSet<ConsentType> RevocableConsentTypes =
    [
        ConsentType.Personalization,
        ConsentType.Marketing,
        ConsentType.Analytics
    ];

    private readonly IUserRepository _users;
    private readonly IPrivacyRepository _privacy;
    private readonly IPasswordHasher _passwords;
    private readonly IUnitOfWork _unitOfWork;
    private readonly PrivacyOptions _options;

    public PrivacyService(
        IUserRepository users,
        IPrivacyRepository privacy,
        IPasswordHasher passwords,
        IUnitOfWork unitOfWork,
        PrivacyOptions options)
    {
        _users = users;
        _privacy = privacy;
        _passwords = passwords;
        _unitOfWork = unitOfWork;
        _options = options;
    }

    public async Task RecordRegistrationAsync(
        User user,
        RegisterRequest request,
        string? ipAddress,
        string? userAgent,
        string correlationId,
        CancellationToken ct = default)
    {
        if (!request.OfferAccepted
            || !request.PrivacyAcknowledged
            || !request.PersonalDataConsentAccepted
            || !request.AgeConfirmed18)
            throw new DomainException("Mandatory registration acknowledgements are missing.");

        user.IsAdultConfirmed = true;
        user.HistoryEnabled = true;
        await _users.UpdateAsync(user, ct);

        var evidence = new RegistrationEvidence(
            NormalizeCollectionSource(request.CollectionSource),
            HashEvidence(ipAddress),
            HashEvidence(userAgent));

        await AddConsentAsync(
            user,
            ConsentType.OfferAcceptance,
            LegalDocumentType.PublicOffer,
            request.DocumentVersions.Offer,
            evidence,
            correlationId,
            ct);
        await AddConsentAsync(
            user,
            ConsentType.PrivacyPolicyAcknowledgement,
            LegalDocumentType.PrivacyPolicy,
            request.DocumentVersions.Privacy,
            evidence,
            correlationId,
            ct);
        await AddConsentAsync(
            user,
            ConsentType.PersonalDataProcessingConsent,
            LegalDocumentType.PersonalDataConsent,
            request.DocumentVersions.PersonalDataConsent,
            evidence,
            correlationId,
            ct);
        await AddConsentAsync(
            user,
            ConsentType.AgeConfirmation,
            LegalDocumentType.PublicOffer,
            request.DocumentVersions.Offer,
            evidence,
            correlationId,
            ct);

        if (request.OptionalConsents.Personalization)
            await AddConsentAsync(user, ConsentType.Personalization, LegalDocumentType.PersonalDataConsent,
                request.DocumentVersions.PersonalDataConsent, evidence, correlationId, ct);
        if (request.OptionalConsents.Marketing)
            await AddConsentAsync(user, ConsentType.Marketing, LegalDocumentType.MarketingConsent,
                request.DocumentVersions.MarketingConsent, evidence, correlationId, ct);
        if (request.OptionalConsents.Analytics)
            await AddConsentAsync(user, ConsentType.Analytics, LegalDocumentType.CookiePolicy,
                request.DocumentVersions.Cookies, evidence, correlationId, ct);
    }

    public async Task<PrivacySettingsDto> GetSettingsAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await GetUserAsync(userId, allowDeletionPending: false, ct);
        var consents = await _privacy.GetConsentsAsync(userId, ct);
        var active = consents.Where(x => x.RevokedAt is null).Select(x => x.ConsentType).ToHashSet();
        return new PrivacySettingsDto
        {
            HistoryEnabled = true,
            AgeConfirmed18 = user.IsAdultConfirmed,
            PersonalizationEnabled = active.Contains(ConsentType.Personalization),
            MarketingEnabled = active.Contains(ConsentType.Marketing),
            AnalyticsEnabled = active.Contains(ConsentType.Analytics)
        };
    }

    public Task<PrivacySettingsDto> UpdateHistoryAsync(
        Guid userId,
        bool enabled,
        string correlationId,
        CancellationToken ct = default)
    {
        // Compatibility endpoint for older clients; saving history is always enabled.
        return GetSettingsAsync(userId, ct);
    }

    public async Task<IReadOnlyList<ConsentDto>> GetConsentsAsync(Guid userId, CancellationToken ct = default)
    {
        await GetUserAsync(userId, allowDeletionPending: false, ct);
        var consents = await _privacy.GetConsentsAsync(userId, ct);
        return consents.Select(MapConsent).ToList();
    }

    public async Task RevokeConsentAsync(
        Guid userId,
        ConsentType type,
        string correlationId,
        CancellationToken ct = default)
    {
        if (!RevocableConsentTypes.Contains(type))
            throw new DomainException("This legal acknowledgement cannot be revoked through the optional-consent endpoint.");

        var user = await GetUserAsync(userId, allowDeletionPending: false, ct);
        var now = DateTime.UtcNow;
        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            var revoked = await _privacy.RevokeConsentAsync(userId, type, now, innerCt);
            var request = CreateRequest(user, DataSubjectRequestType.RevokeConsent, now, identityVerifiedAt: null);
            CompleteRequest(request, now, revoked ? "consent_revoked" : "already_inactive");
            await _privacy.AddDataSubjectRequestAsync(request, innerCt);
            await AuditAsync(user, "privacy.consent_revoked", "consent", null, "success",
                type.ToString().ToLowerInvariant(), correlationId, innerCt);
            return true;
        }, ct);
    }

    public async Task<PrivacyExportDto> ExportAsync(
        Guid userId,
        string password,
        string correlationId,
        CancellationToken ct = default)
    {
        if (!_options.ExportEnabled)
            throw new FeatureDisabledException(
                "privacy_export",
                "Экспорт данных временно отключён оператором сервиса.");
        var user = await ReauthenticateAsync(userId, password, ct);
        var now = DateTime.UtcNow;
        var request = CreateRequest(user, DataSubjectRequestType.Export, now, now);
        request.Status = DataSubjectRequestStatus.InProgress;
        await _privacy.AddDataSubjectRequestAsync(request, ct);

        var readings = await _privacy.GetReadingsForExportAsync(userId, ct);
        var feedbacks = await _privacy.GetFeedbacksForExportAsync(userId, ct);
        var memory = await _privacy.GetMemoryForExportAsync(userId, ct);
        var achievements = await _privacy.GetAchievementsForExportAsync(userId, ct);
        var announcementReads = await _privacy.GetAnnouncementReadsForExportAsync(userId, ct);
        var consents = await _privacy.GetConsentsAsync(userId, ct);
        var payments = await _privacy.GetPaymentsAsync(user.PrivacySubjectId, ct);
        var paymentOrders = await _privacy.GetPaymentOrdersAsync(user.PrivacySubjectId, ct) ?? [];
        var requests = await _privacy.GetDataSubjectRequestsAsync(userId, ct);

        CompleteRequest(request, DateTime.UtcNow, request.Id.ToString("N"));
        await _privacy.UpdateDataSubjectRequestAsync(request, ct);
        await AuditAsync(user, "privacy.export_completed", "data_subject_request", request.Id,
            "success", null, correlationId, ct);

        return new PrivacyExportDto
        {
            GeneratedAt = DateTime.UtcNow,
            Profile = new PrivacyProfileExportDto
            {
                Id = user.Id,
                Email = user.Email,
                CreatedAt = user.CreatedAt,
                FirstName = user.FirstName,
                LastName = user.LastName,
                BirthYear = user.BirthYear,
                AgeConfirmed18 = user.IsAdultConfirmed,
                HistoryEnabled = user.HistoryEnabled,
                AccountStatus = user.AccountStatus.ToString(),
                SubscriptionStatus = user.SubscriptionStatus.ToString(),
                SubscriptionExpiresAt = user.SubscriptionExpiresAt,
                HasUsedIntroReading = user.HasUsedIntroReading,
                LastReadingAt = user.LastReadingAt,
                IsEmailVerified = user.IsEmailVerified,
                IsAdmin = user.IsAdmin
            },
            Readings = readings.Select(x => new PrivacyReadingExportDto
            {
                Id = x.Id,
                SpreadType = x.SpreadType.ToString(),
                DeckType = x.DeckType.ToString(),
                Question = x.Question,
                CreatedAt = x.CreatedAt,
                SavedToHistory = x.SavedToHistory,
                DeletedFromHistoryAt = x.DeletedFromHistoryAt,
                AiInterpretation = x.AiInterpretation,
                AiModel = x.AiModel,
                Cards = x.Cards.OrderBy(c => c.Position).Select(c => new PrivacyReadingCardExportDto
                {
                    CardId = c.CardId,
                    Position = c.Position,
                    IsReversed = c.IsReversed
                }).ToList()
            }).ToList(),
            Feedbacks = feedbacks.Select(x => new PrivacyFeedbackExportDto
            {
                Id = x.Id,
                ReadingId = x.ReadingId,
                SelfReport = x.SelfReport,
                AiScore = x.AiScore,
                AiScoreReason = x.AiScoreReason,
                IsSincere = x.IsSincere,
                Status = x.Status.ToString(),
                CreatedAt = x.CreatedAt,
                ScheduledAt = x.ScheduledAt,
                NotifiedAt = x.NotifiedAt,
                AnsweredAt = x.AnsweredAt
            }).ToList(),
            MemoryRules = memory.Select(x => new UserMemoryRuleDto
            {
                Id = x.Id,
                Text = x.Text,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            }).ToList(),
            Achievements = achievements.Select(x => new PrivacyAchievementExportDto
            {
                AchievementId = x.AchievementId,
                Code = x.Achievement?.Code,
                UnlockedAt = x.UnlockedAt
            }).ToList(),
            AnnouncementReads = announcementReads.Select(x => new PrivacyAnnouncementReadExportDto
            {
                AnnouncementId = x.AnnouncementId,
                ReadAt = x.ReadAt
            }).ToList(),
            Consents = consents.Select(MapConsent).ToList(),
            Payments = paymentOrders.Select(x => new PrivacyPaymentExportDto
                {
                    OrderId = x.PublicId.ToString("N"),
                    ProviderPaymentId = x.ProviderPaymentId,
                    Provider = x.Provider,
                    TariffCode = x.TariffCode,
                    Amount = x.Amount,
                    Currency = x.Currency,
                    AccessDays = x.AccessDays,
                    Status = x.Status.ToString(),
                    ReceiptStatus = x.ReceiptStatus.ToString(),
                    CreatedAt = x.CreatedAt
                })
                .Concat(payments
                    .Where(x => paymentOrders.All(o => o.ProviderPaymentId != x.PaymentId))
                    .Select(x => new PrivacyPaymentExportDto
                    {
                        OrderId = x.PaymentId,
                        ProviderPaymentId = x.PaymentId,
                        Status = "LegacyProcessed",
                        CreatedAt = x.ProcessedAt
                    }))
                .OrderByDescending(x => x.CreatedAt)
                .ToList(),
            Requests = requests.Select(x => new PrivacyRequestExportDto
            {
                Id = x.Id,
                RequestType = x.RequestType.ToString(),
                ReceivedAt = x.ReceivedAt,
                IdentityVerifiedAt = x.IdentityVerifiedAt,
                DueAt = x.DueAt,
                Status = x.Id == request.Id ? DataSubjectRequestStatus.Completed.ToString() : x.Status.ToString(),
                CompletedAt = x.Id == request.Id ? request.CompletedAt : x.CompletedAt
            }).ToList()
        };
    }

    public Task DeleteReadingAsync(
        Guid userId,
        Guid readingId,
        string password,
        string correlationId,
        CancellationToken ct = default) =>
        DeleteReadingsCoreAsync(userId, readingId, password, correlationId, ct);

    public Task DeleteAllReadingsAsync(
        Guid userId,
        string password,
        string correlationId,
        CancellationToken ct = default) =>
        DeleteReadingsCoreAsync(userId, null, password, correlationId, ct);

    public async Task<AccountDeletionStatusDto> RequestAccountDeletionAsync(
        Guid userId,
        string password,
        string correlationId,
        CancellationToken ct = default)
    {
        var user = await ReauthenticateAsync(userId, password, ct);
        var existing = await _privacy.GetLatestDeletionJobAsync(userId, ct);
        if (existing is { Status: DataDeletionJobStatus.Pending or DataDeletionJobStatus.Running })
            return MapDeletionStatus(existing);

        var now = DateTime.UtcNow;
        var job = new DataDeletionJob
        {
            UserId = user.Id,
            SubjectReference = user.PrivacySubjectId,
            RequestedAt = now,
            ScheduledAt = now.AddHours(Math.Max(0, _options.AccountDeletionGracePeriodHours))
        };
        var request = CreateRequest(user, DataSubjectRequestType.AccountDeletion, now, now);
        request.Status = DataSubjectRequestStatus.InProgress;
        request.ResultReference = job.Id.ToString("N");

        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            user.AccountStatus = UserAccountStatus.DeletionPending;
            user.AccountDeletionRequestedAt = now;
            user.SecurityVersion++;
            user.EmailVerificationToken = null;
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpiresAt = null;
            await _users.UpdateAsync(user, innerCt);
            await _privacy.AddDataSubjectRequestAsync(request, innerCt);
            await _privacy.AddDeletionJobAsync(job, innerCt);
            await AuditAsync(user, "privacy.account_deletion_requested", "data_deletion_job", job.Id,
                "success", null, correlationId, innerCt);
            return true;
        }, ct);

        return MapDeletionStatus(job);
    }

    public async Task<AccountDeletionStatusDto> GetAccountDeletionStatusAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        await GetUserAsync(userId, allowDeletionPending: true, ct);
        var job = await _privacy.GetLatestDeletionJobAsync(userId, ct);
        return job is null ? new AccountDeletionStatusDto { Requested = false } : MapDeletionStatus(job);
    }

    private async Task DeleteReadingsCoreAsync(
        Guid userId,
        Guid? readingId,
        string password,
        string correlationId,
        CancellationToken ct)
    {
        var user = await ReauthenticateAsync(userId, password, ct);
        var now = DateTime.UtcNow;
        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            var deleted = readingId.HasValue
                ? await _privacy.DeleteReadingAsync(userId, readingId.Value, innerCt) ? 1 : 0
                : await _privacy.DeleteReadingsAsync(userId, innerCt);

            if (readingId.HasValue && deleted == 0)
                throw new NotFoundException("Reading not found");

            var request = CreateRequest(
                user,
                readingId.HasValue ? DataSubjectRequestType.DeleteReading : DataSubjectRequestType.DeleteHistory,
                now,
                now);
            CompleteRequest(request, now, deleted == 0 ? "nothing_to_delete" : "deleted");
            await _privacy.AddDataSubjectRequestAsync(request, innerCt);
            await AuditAsync(user,
                readingId.HasValue ? "privacy.reading_deleted" : "privacy.history_deleted",
                readingId.HasValue ? "reading" : "reading_history",
                readingId,
                "success",
                deleted == 0 ? "nothing_to_delete" : null,
                correlationId,
                innerCt);
            return true;
        }, ct);
    }

    private async Task<User> ReauthenticateAsync(Guid userId, string password, CancellationToken ct)
    {
        var user = await GetUserAsync(userId, allowDeletionPending: false, ct);
        if (string.IsNullOrWhiteSpace(password) || !_passwords.Verify(password, user.PasswordHash))
            throw new ReauthenticationFailedException();
        return user;
    }

    private async Task<User> GetUserAsync(Guid userId, bool allowDeletionPending, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new UnauthorizedException("Authentication required");
        if (!allowDeletionPending && user.AccountStatus != UserAccountStatus.Active)
            throw new UnauthorizedException("Account is blocked");
        return user;
    }

    private DataSubjectRequest CreateRequest(
        User user,
        DataSubjectRequestType type,
        DateTime receivedAt,
        DateTime? identityVerifiedAt) => new()
    {
        UserId = user.Id,
        SubjectReference = user.PrivacySubjectId,
        RequestType = type,
        ReceivedAt = receivedAt,
        IdentityVerifiedAt = identityVerifiedAt,
        DueAt = receivedAt.AddDays(Math.Max(1, _options.DataSubjectRequestDueDays))
    };

    private static void CompleteRequest(DataSubjectRequest request, DateTime completedAt, string resultReference)
    {
        request.Status = DataSubjectRequestStatus.Completed;
        request.CompletedAt = completedAt;
        request.ResultReference = resultReference;
    }

    private async Task AddConsentAsync(
        User user,
        ConsentType consentType,
        LegalDocumentType documentType,
        string documentVersion,
        RegistrationEvidence evidence,
        string correlationId,
        CancellationToken ct)
    {
        var document = await _privacy.GetActiveLegalDocumentAsync(documentType, documentVersion, ct)
            ?? throw new ConflictException(
                $"Legal document {documentType} version {documentVersion} is unavailable or no longer active.");
        var now = DateTime.UtcNow;
        await _privacy.AddConsentAsync(new UserConsent
        {
            UserId = user.Id,
            SubjectReference = user.PrivacySubjectId,
            ConsentType = consentType,
            LegalDocumentId = document.Id,
            DocumentVersion = document.Version,
            ContentHash = document.ContentHash,
            AcceptedAt = now,
            CollectionSource = evidence.CollectionSource,
            IpHash = evidence.IpHash,
            UserAgentHash = evidence.UserAgentHash
        }, ct);
        await AuditAsync(user, "privacy.consent_recorded", "legal_document", document.Id,
            "success", consentType.ToString().ToLowerInvariant(), correlationId, ct, document.Version);
    }

    private Task AuditAsync(
        User user,
        string eventType,
        string targetType,
        Guid? targetReference,
        string outcome,
        string? reasonCode,
        string correlationId,
        CancellationToken ct,
        string? documentVersion = null) =>
        _privacy.AddAuditEventAsync(new AuditEvent
        {
            OccurredAt = DateTime.UtcNow,
            EventType = eventType,
            CorrelationId = NormalizeCorrelationId(correlationId),
            ActorSubjectReference = user.PrivacySubjectId,
            TargetType = targetType,
            TargetReference = targetReference,
            Outcome = outcome,
            ReasonCode = reasonCode,
            DocumentVersion = documentVersion
        }, ct);

    private string? HashEvidence(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || _options.EvidenceHashKey.Length < 32)
            return null;
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.EvidenceHashKey));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(value.Trim()))).ToLowerInvariant();
    }

    private static string NormalizeCollectionSource(string source)
    {
        var normalized = source.Trim();
        if (normalized.Length is 0 or > 64
            || normalized.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')))
            throw new DomainException("Invalid consent collection source.");
        return normalized;
    }

    private static string NormalizeCorrelationId(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Guid.NewGuid().ToString("N");
        var normalized = value.Trim();
        return normalized[..Math.Min(normalized.Length, 64)];
    }

    private static ConsentDto MapConsent(UserConsent x) => new()
    {
        Id = x.Id,
        ConsentType = x.ConsentType.ToString(),
        DocumentVersion = x.DocumentVersion,
        ContentHash = x.ContentHash,
        AcceptedAt = x.AcceptedAt,
        RevokedAt = x.RevokedAt,
        CollectionSource = x.CollectionSource
    };

    private static AccountDeletionStatusDto MapDeletionStatus(DataDeletionJob x) => new()
    {
        Requested = true,
        Status = x.Status.ToString(),
        RequestedAt = x.RequestedAt,
        ScheduledAt = x.ScheduledAt,
        StartedAt = x.StartedAt,
        CompletedAt = x.CompletedAt
    };

    private sealed record RegistrationEvidence(string CollectionSource, string? IpHash, string? UserAgentHash);
}
