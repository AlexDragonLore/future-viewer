namespace FutureViewer.DomainServices.DTOs;

public sealed class CreatePaymentRequest
{
    public bool OfferAccepted { get; init; }
    public string OfferVersion { get; init; } = string.Empty;
}
