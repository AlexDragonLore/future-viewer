using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Interfaces;
using FutureViewer.Infrastructure.BackgroundServices;
using FutureViewer.Infrastructure.Persistence;
using FutureViewer.Integration.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class PrivacyEndpointTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public PrivacyEndpointTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Registration_without_optional_choices_records_only_mandatory_document_versions()
    {
        var client = _fixture.CreateClient();
        var email = $"consent-{Guid.NewGuid():N}@example.com";

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            AuthTestExtensions.CreateRegistrationRequest(email, "password123"));

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(x => x.Email == email);
        user.IsAdultConfirmed.Should().BeTrue();
        user.HistoryEnabled.Should().BeFalse();
        var consents = await db.UserConsents.Where(x => x.UserId == user.Id).ToListAsync();
        consents.Select(x => x.ConsentType).Should().BeEquivalentTo(new[]
        {
            ConsentType.OfferAcceptance,
            ConsentType.PrivacyPolicyAcknowledgement,
            ConsentType.PersonalDataProcessingConsent,
            ConsentType.AgeConfirmation
        });
        consents.Should().OnlyContain(x => x.DocumentVersion == AuthTestExtensions.LegalDocumentVersion);
        consents.Should().OnlyContain(x => x.ContentHash == new string('a', 64));
    }

    [Fact]
    public async Task Registration_without_personal_data_processing_consent_is_rejected()
    {
        var client = _fixture.CreateClient();
        var email = $"missing-pd-consent-{Guid.NewGuid():N}@example.com";
        var valid = AuthTestExtensions.CreateRegistrationRequest(email, "password123");
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

        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Users.AnyAsync(x => x.Email == email)).Should().BeFalse();
    }

    [Fact]
    public async Task Registration_with_unknown_document_version_is_rejected_and_rolled_back()
    {
        var client = _fixture.CreateClient();
        var email = $"stale-consent-{Guid.NewGuid():N}@example.com";
        var request = AuthTestExtensions.CreateRegistrationRequest(email, "password123");
        request = new RegisterRequest
        {
            Email = request.Email,
            Password = request.Password,
            OfferAccepted = true,
            PrivacyAcknowledged = true,
            PersonalDataConsentAccepted = true,
            AgeConfirmed18 = true,
            DocumentVersions = new LegalDocumentVersionsDto
            {
                Offer = "stale",
                Privacy = request.DocumentVersions.Privacy,
                PersonalDataConsent = request.DocumentVersions.PersonalDataConsent,
                MarketingConsent = request.DocumentVersions.MarketingConsent,
                Cookies = request.DocumentVersions.Cookies
            },
            CollectionSource = "registration"
        };

        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Users.AnyAsync(x => x.Email == email)).Should().BeFalse();
    }

    [Fact]
    public async Task Settings_and_export_are_owner_scoped_and_export_requires_password()
    {
        var (client, auth, email) = await CreateAuthenticatedClientAsync();

        var settingsResponse = await client.GetAsync("/api/privacy/settings");
        settingsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var settings = await settingsResponse.Content.ReadFromJsonAsync<PrivacySettingsDto>();
        settings!.HistoryEnabled.Should().BeFalse();
        settings.MarketingEnabled.Should().BeFalse();

        var unauthorized = await client.PostAsJsonAsync(
            "/api/privacy/export", new ReauthenticationRequest { Password = "wrong-password" });
        unauthorized.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await unauthorized.Content.ReadAsStringAsync()).Should().Contain("reauthentication_failed");
        (await client.GetAsync("/api/privacy/settings")).StatusCode.Should().Be(HttpStatusCode.OK,
            "an incorrect confirmation password must not revoke the authenticated session");

        var exportResponse = await client.PostAsJsonAsync(
            "/api/privacy/export", new ReauthenticationRequest { Password = "password123" });
        exportResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var export = await exportResponse.Content.ReadFromJsonAsync<PrivacyExportDto>();
        export!.Profile.Id.Should().Be(auth.UserId);
        export.Profile.Email.Should().Be(email);
        export.Consents.Should().HaveCount(4);
    }

    [Fact]
    public async Task Export_includes_all_retained_content_and_only_the_requesting_users_records()
    {
        var (client, auth, _) = await CreateAuthenticatedClientAsync();
        var hiddenReading = new Reading
        {
            UserId = auth.UserId,
            SpreadType = SpreadType.SingleCard,
            Question = "Archived question",
            SavedToHistory = true,
            DeletedFromHistoryAt = DateTime.UtcNow,
            AiInterpretation = "Archived answer"
        };
        var transientReading = new Reading
        {
            UserId = auth.UserId,
            SpreadType = SpreadType.SingleCard,
            Question = string.Empty,
            SavedToHistory = false
        };
        var other = new User { Email = $"other-export-{Guid.NewGuid():N}@example.com", PasswordHash = "not-exported" };
        var otherReading = new Reading
        {
            UserId = other.Id,
            SpreadType = SpreadType.SingleCard,
            Question = "Another person's question",
            SavedToHistory = true
        };
        var achievement = new Achievement
        {
            Code = $"export-{Guid.NewGuid():N}",
            NameRu = "Test achievement",
            DescriptionRu = "Export fixture",
            IconPath = "test"
        };
        var ownAnnouncementId = Guid.NewGuid();
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Add(other);
            db.Readings.AddRange(hiddenReading, transientReading, otherReading);
            db.Achievements.Add(achievement);
            foreach (var (userId, reading, prefix) in new[]
                     {
                         (auth.UserId, hiddenReading, "Own"), (other.Id, otherReading, "Other")
                     })
            {
                db.ReadingFeedbacks.Add(new ReadingFeedback
                {
                    UserId = userId,
                    ReadingId = reading.Id,
                    Token = $"secret-token-{Guid.NewGuid():N}",
                    SelfReport = $"{prefix} feedback",
                    AiScore = 7,
                    AiScoreReason = $"{prefix} score reason",
                    ScheduledAt = DateTime.UtcNow,
                    Status = FeedbackStatus.Scored
                });
                db.UserMemoryRules.Add(new UserMemoryRule { UserId = userId, Text = $"{prefix} memory" });
                db.UserAchievements.Add(new UserAchievement { UserId = userId, AchievementId = achievement.Id });
                var announcementId = userId == auth.UserId ? ownAnnouncementId : Guid.NewGuid();
                db.Announcements.Add(new Announcement
                {
                    Id = announcementId,
                    Code = $"export-{announcementId:N}",
                    Title = "Test announcement",
                    Body = "Test content"
                });
                db.AnnouncementReads.Add(new AnnouncementRead
                {
                    UserId = userId,
                    AnnouncementId = announcementId
                });
            }
            await db.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync(
            "/api/privacy/export", new ReauthenticationRequest { Password = "password123" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var export = await response.Content.ReadFromJsonAsync<PrivacyExportDto>();
        export!.Readings.Select(x => x.Id).Should().BeEquivalentTo(new[] { hiddenReading.Id, transientReading.Id });
        export.Readings.Should().ContainSingle(x => x.Id == hiddenReading.Id && x.DeletedFromHistoryAt != null);
        export.Feedbacks.Should().ContainSingle(x => x.SelfReport == "Own feedback" && x.AiScoreReason == "Own score reason");
        export.MemoryRules.Should().ContainSingle(x => x.Text == "Own memory");
        export.Achievements.Should().ContainSingle(x => x.AchievementId == achievement.Id);
        export.AnnouncementReads.Should().ContainSingle(x => x.AnnouncementId == ownAnnouncementId);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().NotContain("secret-token-").And.NotContain("passwordHash").And.NotContain(other.Email);
    }

    [Fact]
    public async Task Privacy_reauthentication_attempts_are_rate_limited_without_blocking_settings()
    {
        var (client, _, _) = await CreateAuthenticatedClientAsync();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var rejected = await client.PostAsJsonAsync(
                "/api/privacy/export", new ReauthenticationRequest { Password = "wrong-password" });
            rejected.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        using var secondClient = _fixture.CreateClient();
        secondClient.DefaultRequestHeaders.Authorization = client.DefaultRequestHeaders.Authorization;
        var limited = await secondClient.PostAsJsonAsync(
            "/api/privacy/export", new ReauthenticationRequest { Password = "wrong-password" });
        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await client.GetAsync("/api/privacy/settings")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Revoking_consent_only_changes_the_owners_active_records()
    {
        var (client, auth, _) = await CreateAuthenticatedClientAsync();
        var other = new User { Email = $"other-consent-{Guid.NewGuid():N}@example.com", PasswordHash = "not-used" };
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(x => x.Id == auth.UserId);
            var document = await db.LegalDocuments.SingleAsync(x => x.DocumentType == LegalDocumentType.MarketingConsent);
            db.Users.Add(other);
            foreach (var (owner, revokedAt) in new[]
                     {
                         (user, (DateTime?)DateTime.UtcNow.AddDays(-1)),
                         (user, (DateTime?)null),
                         (other, (DateTime?)null)
                     })
                db.UserConsents.Add(new UserConsent
                {
                    UserId = owner.Id,
                    SubjectReference = owner.PrivacySubjectId,
                    ConsentType = ConsentType.Marketing,
                    LegalDocumentId = document.Id,
                    DocumentVersion = document.Version,
                    ContentHash = document.ContentHash,
                    AcceptedAt = DateTime.UtcNow,
                    RevokedAt = revokedAt,
                    CollectionSource = "registration"
                });
            await db.SaveChangesAsync();
        }

        var response = await client.PostAsync("/api/privacy/consents/marketing/revoke", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var checkScope = _fixture.Services.CreateScope();
        var checkDb = checkScope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await checkDb.UserConsents.CountAsync(x => x.UserId == auth.UserId && x.ConsentType == ConsentType.Marketing && x.RevokedAt == null))
            .Should().Be(0);
        (await checkDb.UserConsents.CountAsync(x => x.UserId == other.Id && x.ConsentType == ConsentType.Marketing && x.RevokedAt == null))
            .Should().Be(1);
    }

    [Fact]
    public async Task Account_deletion_recovers_crashed_and_failed_jobs_but_leaves_live_workers_alone()
    {
        var now = DateTime.UtcNow;
        var users = Enumerable.Range(0, 3).Select(_ => new User
        {
            Email = $"recover-delete-{Guid.NewGuid():N}@example.com",
            PasswordHash = "not-used",
            AccountStatus = UserAccountStatus.DeletionPending
        }).ToArray();
        var jobs = users.Select((user, index) => new DataDeletionJob
        {
            UserId = user.Id,
            SubjectReference = user.PrivacySubjectId,
            RequestedAt = now.AddHours(-1),
            ScheduledAt = now.AddHours(-1),
            StartedAt = index == 2 ? now : now.AddHours(-1),
            Status = index == 1 ? DataDeletionJobStatus.Failed : DataDeletionJobStatus.Running,
            FailureReason = index == 1 ? "processor_error" : null
        }).ToArray();
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.AddRange(users);
            db.DataDeletionJobs.AddRange(jobs);
            await db.SaveChangesAsync();
        }

        using (var scope = _fixture.Services.CreateScope())
            (await scope.ServiceProvider.GetRequiredService<AccountDeletionProcessor>().ProcessBatchAsync()).Should().Be(2);

        using var checkScope = _fixture.Services.CreateScope();
        var checkDb = checkScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var jobIds = jobs.Select(x => x.Id).ToArray();
        var stored = await checkDb.DataDeletionJobs.Where(x => jobIds.Contains(x.Id)).ToListAsync();
        stored.Where(x => x.Id != jobs[2].Id).Should().OnlyContain(x => x.Status == DataDeletionJobStatus.Completed);
        stored.Single(x => x.Id == jobs[2].Id).Status.Should().Be(DataDeletionJobStatus.Running);
        (await checkDb.Users.AnyAsync(x => x.Id == users[2].Id)).Should().BeTrue();
        (await checkDb.Users.AnyAsync(x => x.Id == users[0].Id || x.Id == users[1].Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Reading_and_history_deletion_are_physical_and_reauthenticated()
    {
        var (client, auth, _) = await CreateAuthenticatedClientAsync();
        await ActivateSubscriptionAsync(auth.UserId);
        var historySetting = await client.PutAsJsonAsync(
            "/api/privacy/settings/history",
            new UpdateHistorySettingRequest { Enabled = true });
        historySetting.StatusCode.Should().Be(HttpStatusCode.OK);
        var createdResponse = await client.PostAsJsonAsync("/api/readings", new CreateReadingRequest
        {
            SpreadType = SpreadType.SingleCard,
            Question = "Что поможет сосредоточиться?",
            SaveToHistory = true
        });
        createdResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createdResponse.Content.ReadFromJsonAsync<ReadingResult>();

        var delete = new HttpRequestMessage(HttpMethod.Delete, $"/api/privacy/readings/{created!.Id}")
        {
            Content = JsonContent.Create(new ReauthenticationRequest { Password = "password123" })
        };
        var deleteResponse = await client.SendAsync(delete);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Readings.AnyAsync(x => x.Id == created.Id)).Should().BeFalse();
        (await db.AuditEvents.AnyAsync(x => x.EventType == "privacy.reading_deleted"
                                             && x.ActorSubjectReference != null)).Should().BeTrue();
    }

    [Fact]
    public async Task Account_deletion_processor_removes_working_data_but_keeps_payment_evidence_pseudonymously()
    {
        var (client, auth, email) = await CreateAuthenticatedClientAsync();
        Guid subjectReference;
        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(x => x.Id == auth.UserId);
            subjectReference = user.PrivacySubjectId;
            db.ProcessedPayments.Add(new ProcessedPayment
            {
                PaymentId = $"payment-{Guid.NewGuid():N}",
                SubjectReference = subjectReference
            });
            await db.SaveChangesAsync();
        }

        var request = await client.PostAsJsonAsync(
            "/api/privacy/account-deletion", new ReauthenticationRequest { Password = "password123" });
        request.StatusCode.Should().Be(HttpStatusCode.Accepted);

        using (var scope = _fixture.Services.CreateScope())
        {
            var processor = scope.ServiceProvider.GetRequiredService<AccountDeletionProcessor>();
            (await processor.ProcessBatchAsync()).Should().Be(1);
        }

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Users.AnyAsync(x => x.Id == auth.UserId)).Should().BeFalse();
            (await db.ProcessedPayments.AnyAsync(x => x.SubjectReference == subjectReference)).Should().BeTrue();
            (await db.UserConsents.AnyAsync(x => x.SubjectReference == subjectReference && x.UserId == null)).Should().BeTrue();
            (await db.DataDeletionJobs.AnyAsync(x => x.SubjectReference == subjectReference
                                                     && x.Status == DataDeletionJobStatus.Completed
                                                     && x.UserId == null)).Should().BeTrue();
            (await db.AuditEvents.AnyAsync(x => x.EventType == "privacy.account_deleted"
                                                && x.ActorSubjectReference == subjectReference)).Should().BeTrue();
        }

        var login = await _fixture.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "password123"
        });
        login.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<(HttpClient Client, AuthResponse Auth, string Email)> CreateAuthenticatedClientAsync()
    {
        var client = _fixture.CreateClient();
        var email = $"privacy-{Guid.NewGuid():N}@example.com";
        var auth = await _fixture.RegisterAndLoginAsync(client, email, "password123");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth, email);
    }

    private async Task ActivateSubscriptionAsync(Guid userId)
    {
        using var scope = _fixture.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var user = await users.GetByIdAsync(userId);
        user!.SubscriptionStatus = SubscriptionStatus.Active;
        user.SubscriptionExpiresAt = DateTime.UtcNow.AddDays(1);
        await users.UpdateAsync(user);
    }
}
