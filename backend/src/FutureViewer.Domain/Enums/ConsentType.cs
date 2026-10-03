namespace FutureViewer.Domain.Enums;

public enum ConsentType
{
    OfferAcceptance = 1,
    PrivacyPolicyAcknowledgement = 2,
    AgeConfirmation = 3,
    PersonalDataProcessingConsent = 4,
    Personalization = 10,
    // Historical records only; this integration has been removed. Do not reuse this value.
    Telegram = 11,
    Marketing = 12,
    Analytics = 13
}
