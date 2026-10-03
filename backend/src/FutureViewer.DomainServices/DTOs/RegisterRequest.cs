namespace FutureViewer.DomainServices.DTOs;

public sealed class RegisterRequest
{
    public required string Email { get; init; }
    public required string Password { get; init; }
    public bool OfferAccepted { get; init; }
    public bool PrivacyAcknowledged { get; init; }
    public bool PersonalDataConsentAccepted { get; init; }
    public bool AgeConfirmed18 { get; init; }
    public LegalDocumentVersionsDto DocumentVersions { get; init; } = new();
    public OptionalConsentSelectionDto OptionalConsents { get; init; } = new();
    public string CollectionSource { get; init; } = "registration";
}

public sealed class LegalDocumentVersionsDto
{
    public string Offer { get; init; } = string.Empty;
    public string Privacy { get; init; } = string.Empty;
    public string PersonalDataConsent { get; init; } = string.Empty;
    public string MarketingConsent { get; init; } = string.Empty;
    public string Cookies { get; init; } = string.Empty;
}

public sealed class OptionalConsentSelectionDto
{
    public bool Personalization { get; init; }
    public bool Marketing { get; init; }
    public bool Analytics { get; init; }
}
