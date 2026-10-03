using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.Infrastructure.Compliance;
using FutureViewer.Infrastructure.Persistence;
using FutureViewer.Integration.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class PublishedLegalDocumentTests(IntegrationTestFixture fixture) : IClassFixture<IntegrationTestFixture>
{
    [Fact]
    public async Task Seed_publishes_real_content_hashes_without_documentary_approval_and_is_idempotent()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await DatabaseInitializer.SeedLegalDocumentsAsync(db);
        var count = await db.LegalDocuments.CountAsync();
        await DatabaseInitializer.SeedLegalDocumentsAsync(db);
        (await db.LegalDocuments.CountAsync()).Should().Be(count);

        var active = await db.LegalDocuments.Where(x => x.IsActive).ToListAsync();
        active.Should().HaveCount(8);
        foreach (var content in PublishedLegalDocuments.All)
        {
            var expectedHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content.Content.GetRawText())));
            var row = active.Single(x => x.DocumentType == content.Type);
            row.ContentHash.Should().Be(expectedHash);
            row.Version.Should().Be($"published-{expectedHash[..24]}");
        }
    }

    [Fact]
    public async Task Browser_catalog_versions_register_successfully_and_record_the_exact_published_hashes()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await DatabaseInitializer.SeedLegalDocumentsAsync(db);
        var client = fixture.CreateClient();
        var catalogResponse = await client.GetAsync("/api/public/legal-documents");
        catalogResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        catalogResponse.Headers.CacheControl!.NoStore.Should().BeTrue();
        var json = JsonDocument.Parse(await catalogResponse.Content.ReadAsStringAsync());
        var documents = json.RootElement.GetProperty("documents").EnumerateArray().ToDictionary(
            entry => entry.GetProperty("documentType").GetString()!, entry => entry);
        documents.Should().HaveCount(8);
        var request = new RegisterRequest
        {
            Email = $"published-legal-{Guid.NewGuid():N}@example.com",
            Password = "password123",
            OfferAccepted = true,
            PrivacyAcknowledged = true,
            PersonalDataConsentAccepted = true,
            AgeConfirmed18 = true,
            CollectionSource = "registration",
            DocumentVersions = new LegalDocumentVersionsDto
            {
                Offer = documents["offer"].GetProperty("version").GetString()!,
                Privacy = documents["privacy"].GetProperty("version").GetString()!,
                PersonalDataConsent = documents["personal-data-consent"].GetProperty("version").GetString()!,
                MarketingConsent = documents["marketing-consent"].GetProperty("version").GetString()!,
                Cookies = documents["cookies"].GetProperty("version").GetString()!
            }
        };
        var response = await client.PostAsJsonAsync("/api/auth/register", request);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var user = await db.Users.SingleAsync(x => x.Email == request.Email);
        var consents = await db.UserConsents.Where(x => x.UserId == user.Id).ToListAsync();
        consents.Should().HaveCount(4);
        foreach (var consent in consents)
        {
            var content = PublishedLegalDocuments.All.Single(x => x.Version == consent.DocumentVersion);
            consent.ContentHash.Should().Be(content.ContentHash);
        }
        fixture.EmailSender.Sent.Should().Contain(x => x.To == request.Email);
    }

    [Fact]
    public async Task Removed_document_versions_cannot_be_used_as_registration_evidence()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await DatabaseInitializer.SeedLegalDocumentsAsync(db);
        var client = fixture.CreateClient();
        var request = AuthTestExtensions.CreateRegistrationRequest($"old-legal-{Guid.NewGuid():N}@example.com", "password123");
        var response = await client.PostAsJsonAsync("/api/auth/register", request);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
