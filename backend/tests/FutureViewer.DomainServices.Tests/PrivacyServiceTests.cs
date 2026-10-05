using FluentAssertions;
using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Exceptions;
using FutureViewer.DomainServices.Interfaces;
using FutureViewer.DomainServices.Services;
using FutureViewer.DomainServices.Validation;
using Moq;

namespace FutureViewer.DomainServices.Tests;

public sealed class PrivacyServiceTests
{
    [Fact]
    public async Task Registration_records_mandatory_evidence_without_optional_consents()
    {
        var fixture = CreateFixture();
        var request = ValidRegistration();
        var captured = new List<UserConsent>();
        fixture.Privacy
            .Setup(x => x.GetActiveLegalDocumentAsync(
                It.IsAny<LegalDocumentType>(), "2026-08-01", It.IsAny<CancellationToken>()))
            .ReturnsAsync((LegalDocumentType type, string _, CancellationToken _) => Document(type));
        fixture.Privacy
            .Setup(x => x.AddConsentAsync(It.IsAny<UserConsent>(), It.IsAny<CancellationToken>()))
            .Callback((UserConsent consent, CancellationToken _) => captured.Add(consent))
            .Returns(Task.CompletedTask);

        await fixture.Sut.RecordRegistrationAsync(
            fixture.User,
            request,
            "127.0.0.1",
            "test-agent",
            "correlation",
            CancellationToken.None);

        fixture.User.IsAdultConfirmed.Should().BeTrue();
        fixture.User.HistoryEnabled.Should().BeTrue();
        captured.Select(x => x.ConsentType).Should().BeEquivalentTo(new[]
        {
            ConsentType.OfferAcceptance,
            ConsentType.PrivacyPolicyAcknowledgement,
            ConsentType.PersonalDataProcessingConsent,
            ConsentType.AgeConfirmation
        });
        captured.Should().OnlyContain(x => x.DocumentVersion == "2026-08-01");
        captured.Should().OnlyContain(x => x.ContentHash == new string('a', 64));
        captured.Should().OnlyContain(x => x.IpHash == null && x.UserAgentHash == null,
            "weak unkeyed hashes must not be persisted when EvidenceHashKey is absent");
        fixture.Privacy.Verify(x => x.GetActiveLegalDocumentAsync(
            LegalDocumentType.PersonalDataConsent,
            "2026-08-01",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Registration_validator_requires_mandatory_choices_but_not_optional_consents()
    {
        var validator = new RegisterRequestValidator();
        var valid = ValidRegistration();
        valid.OptionalConsents.Personalization.Should().BeFalse();
        valid.OptionalConsents.Marketing.Should().BeFalse();
        valid.OptionalConsents.Analytics.Should().BeFalse();

        validator.Validate(valid).IsValid.Should().BeTrue();
        validator.Validate(new RegisterRequest
        {
            Email = "person@example.com",
            Password = "password123",
            DocumentVersions = valid.DocumentVersions
        }).IsValid.Should().BeFalse();

        validator.Validate(new RegisterRequest
        {
            Email = valid.Email,
            Password = valid.Password,
            OfferAccepted = true,
            PrivacyAcknowledged = true,
            PersonalDataConsentAccepted = false,
            AgeConfirmed18 = true,
            DocumentVersions = valid.DocumentVersions,
            CollectionSource = valid.CollectionSource
        }).Errors.Should().ContainSingle(x => x.PropertyName == nameof(RegisterRequest.PersonalDataConsentAccepted));
    }

    [Fact]
    public async Task Registration_service_rejects_missing_personal_data_processing_consent()
    {
        var fixture = CreateFixture();
        var valid = ValidRegistration();
        var request = new RegisterRequest
        {
            Email = valid.Email,
            Password = valid.Password,
            OfferAccepted = true,
            PrivacyAcknowledged = true,
            PersonalDataConsentAccepted = false,
            AgeConfirmed18 = true,
            DocumentVersions = valid.DocumentVersions,
            OptionalConsents = valid.OptionalConsents,
            CollectionSource = valid.CollectionSource
        };

        var act = () => fixture.Sut.RecordRegistrationAsync(
            fixture.User,
            request,
            null,
            null,
            "correlation",
            CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("Mandatory registration acknowledgements are missing.");
        fixture.Privacy.Verify(x => x.AddConsentAsync(
            It.IsAny<UserConsent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Optional_consent_can_be_revoked_and_request_has_configured_due_date()
    {
        var fixture = CreateFixture();
        DataSubjectRequest? capturedRequest = null;
        fixture.Privacy
            .Setup(x => x.RevokeConsentAsync(
                fixture.User.Id, ConsentType.Marketing, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        fixture.Privacy
            .Setup(x => x.AddDataSubjectRequestAsync(It.IsAny<DataSubjectRequest>(), It.IsAny<CancellationToken>()))
            .Callback((DataSubjectRequest request, CancellationToken _) => capturedRequest = request)
            .Returns(Task.CompletedTask);

        await fixture.Sut.RevokeConsentAsync(
            fixture.User.Id, ConsentType.Marketing, "correlation", CancellationToken.None);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Status.Should().Be(DataSubjectRequestStatus.Completed);
        capturedRequest.DueAt.Should().BeCloseTo(capturedRequest.ReceivedAt.AddDays(7), TimeSpan.FromSeconds(1));
        fixture.Privacy.Verify(x => x.AddAuditEventAsync(
            It.Is<AuditEvent>(e => e.EventType == "privacy.consent_revoked"
                                   && e.ActorSubjectReference == fixture.User.PrivacySubjectId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Export_requires_password_and_reads_payments_by_pseudonym()
    {
        var fixture = CreateFixture();
        fixture.Passwords.Setup(x => x.Verify("correct", fixture.User.PasswordHash)).Returns(true);
        fixture.Privacy.Setup(x => x.GetReadingsForExportAsync(fixture.User.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Reading>());
        fixture.Privacy.Setup(x => x.GetFeedbacksForExportAsync(fixture.User.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ReadingFeedback>());
        fixture.Privacy.Setup(x => x.GetMemoryForExportAsync(fixture.User.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserMemoryRule>());
        fixture.Privacy.Setup(x => x.GetAchievementsForExportAsync(fixture.User.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserAchievement>());
        fixture.Privacy.Setup(x => x.GetAnnouncementReadsForExportAsync(fixture.User.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AnnouncementRead>());
        fixture.Privacy.Setup(x => x.GetConsentsAsync(fixture.User.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserConsent>());
        fixture.Privacy.Setup(x => x.GetPaymentsAsync(fixture.User.PrivacySubjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ProcessedPayment
                {
                    PaymentId = "payment-1",
                    SubjectReference = fixture.User.PrivacySubjectId
                }
            });
        fixture.Privacy.Setup(x => x.GetPaymentOrdersAsync(
                fixture.User.PrivacySubjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PaymentOrder>());
        fixture.Privacy.Setup(x => x.GetDataSubjectRequestsAsync(fixture.User.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DataSubjectRequest>());

        var export = await fixture.Sut.ExportAsync(
            fixture.User.Id, "correct", "correlation", CancellationToken.None);

        export.Profile.Email.Should().Be(fixture.User.Email);
        export.Payments.Should().ContainSingle(x => x.OrderId == "payment-1");
        fixture.Privacy.Verify(x => x.GetPaymentsAsync(
            fixture.User.PrivacySubjectId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Account_deletion_blocks_user_rotates_security_version_and_queues_pseudonymous_job()
    {
        var fixture = CreateFixture();
        fixture.Passwords.Setup(x => x.Verify("correct", fixture.User.PasswordHash)).Returns(true);
        fixture.Privacy.Setup(x => x.GetLatestDeletionJobAsync(fixture.User.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DataDeletionJob?)null);
        DataDeletionJob? capturedJob = null;
        fixture.Privacy
            .Setup(x => x.AddDeletionJobAsync(It.IsAny<DataDeletionJob>(), It.IsAny<CancellationToken>()))
            .Callback((DataDeletionJob job, CancellationToken _) => capturedJob = job)
            .Returns(Task.CompletedTask);

        var status = await fixture.Sut.RequestAccountDeletionAsync(
            fixture.User.Id, "correct", "correlation", CancellationToken.None);

        status.Requested.Should().BeTrue();
        fixture.User.AccountStatus.Should().Be(UserAccountStatus.DeletionPending);
        fixture.User.SecurityVersion.Should().Be(2);
        capturedJob.Should().NotBeNull();
        capturedJob!.UserId.Should().Be(fixture.User.Id);
        capturedJob.SubjectReference.Should().Be(fixture.User.PrivacySubjectId);
    }

    private static TestFixture CreateFixture()
    {
        var users = new Mock<IUserRepository>();
        var privacy = new Mock<IPrivacyRepository>();
        var passwords = new Mock<IPasswordHasher>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork
            .Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task<bool>>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task<bool>> work, CancellationToken ct) => work(ct));

        var user = new User
        {
            Email = "person@example.com",
            PasswordHash = "password-hash",
            IsEmailVerified = true
        };
        users.Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        users.Setup(x => x.UpdateAsync(user, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        privacy.Setup(x => x.AddAuditEventAsync(It.IsAny<AuditEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        privacy.Setup(x => x.AddDataSubjectRequestAsync(It.IsAny<DataSubjectRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        privacy.Setup(x => x.UpdateDataSubjectRequestAsync(It.IsAny<DataSubjectRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new PrivacyService(
            users.Object,
            privacy.Object,
            passwords.Object,
            unitOfWork.Object,
            new PrivacyOptions { DataSubjectRequestDueDays = 7 });
        return new TestFixture(sut, user, privacy, passwords);
    }

    private static RegisterRequest ValidRegistration() => new()
    {
        Email = "person@example.com",
        Password = "password123",
        OfferAccepted = true,
        PrivacyAcknowledged = true,
        PersonalDataConsentAccepted = true,
        AgeConfirmed18 = true,
        DocumentVersions = new LegalDocumentVersionsDto
        {
            Offer = "2026-08-01",
            Privacy = "2026-08-01",
            PersonalDataConsent = "2026-08-01",
            MarketingConsent = "2026-08-01",
            Cookies = "2026-08-01"
        },
        OptionalConsents = new OptionalConsentSelectionDto(),
        CollectionSource = "registration"
    };

    private static LegalDocument Document(LegalDocumentType type) => new()
    {
        DocumentType = type,
        Version = "2026-08-01",
        ContentHash = new string('a', 64),
        PublishedAt = DateTime.UtcNow.AddDays(-1),
        EffectiveAt = DateTime.UtcNow.AddDays(-1),
        IsActive = true
    };

    private sealed record TestFixture(
        PrivacyService Sut,
        User User,
        Mock<IPrivacyRepository> Privacy,
        Mock<IPasswordHasher> Passwords);
}
