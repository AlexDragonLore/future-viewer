namespace FutureViewer.DomainServices.DTOs;

public sealed class PaymentCreationDto
{
    public required string PaymentId { get; init; }
    public required string ConfirmationUrl { get; init; }
    public required string Status { get; init; }
}
