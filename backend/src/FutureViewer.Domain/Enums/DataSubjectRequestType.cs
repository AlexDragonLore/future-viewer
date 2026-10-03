namespace FutureViewer.Domain.Enums;

public enum DataSubjectRequestType
{
    Export = 1,
    DeleteReading = 2,
    DeleteHistory = 3,
    RevokeConsent = 4,
    // Historical records only; this integration has been removed. Do not reuse this value.
    DisconnectTelegram = 5,
    AccountDeletion = 6
}
