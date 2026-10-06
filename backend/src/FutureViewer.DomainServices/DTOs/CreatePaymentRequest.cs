namespace FutureViewer.DomainServices.DTOs;

public sealed class CreatePaymentRequest
{
    public string TariffCode { get; init; } = "pro-30d";
    public bool OfferAccepted { get; init; }
    public string OfferVersion { get; init; } = string.Empty;
}
