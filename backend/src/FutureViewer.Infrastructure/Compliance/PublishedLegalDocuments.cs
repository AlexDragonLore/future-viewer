using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FutureViewer.Domain.Enums;

namespace FutureViewer.Infrastructure.Compliance;

public sealed record PublishedLegalDocument(string DocumentType, LegalDocumentType Type, JsonElement Content)
{
    public string ContentHash { get; } = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Content.GetRawText())));
    public string Version => $"published-{ContentHash[..24]}";

    public object ToResponse(DateTime effectiveAt) => new
    {
        documentType = DocumentType,
        version = Version,
        contentHash = ContentHash,
        effectiveAt = effectiveAt.ToString("yyyy-MM-dd"),
        title = Content.GetProperty("title").GetString(),
        intro = Content.GetProperty("intro").GetString(),
        sections = Content.GetProperty("sections")
    };
}

public static class PublishedLegalDocuments
{
    public static readonly IReadOnlyList<PublishedLegalDocument> All = Read();

    private static IReadOnlyList<PublishedLegalDocument> Read()
    {
        using var stream = typeof(PublishedLegalDocuments).Assembly.GetManifestResourceStream(
            "FutureViewer.Infrastructure.Compliance.published-legal-documents.json")
            ?? throw new InvalidOperationException("Published legal documents are missing.");
        using var json = JsonDocument.Parse(stream);
        return json.RootElement.GetProperty("documents").EnumerateArray()
            .Select(entry => new PublishedLegalDocument(
                entry.GetProperty("documentType").GetString()!,
                ParseType(entry.GetProperty("documentType").GetString()!),
                entry.GetProperty("content").Clone()))
            .ToArray();
    }

    private static LegalDocumentType ParseType(string value) => value switch
    {
        "offer" => LegalDocumentType.PublicOffer,
        "privacy" => LegalDocumentType.PrivacyPolicy,
        "personal-data-consent" => LegalDocumentType.PersonalDataConsent,
        "marketing-consent" => LegalDocumentType.MarketingConsent,
        "cookies" => LegalDocumentType.CookiePolicy,
        "ai-disclaimer" => LegalDocumentType.AiDisclaimer,
        "data-request" => LegalDocumentType.DataRequestPolicy,
        "processors" => LegalDocumentType.ProcessorRegistry,
        _ => throw new InvalidOperationException($"Unknown legal document type: {value}")
    };
}
