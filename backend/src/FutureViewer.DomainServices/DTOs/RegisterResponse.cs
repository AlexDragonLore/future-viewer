namespace FutureViewer.DomainServices.DTOs;

public sealed class RegisterResponse
{
    public required Guid UserId { get; init; }
    public required string Email { get; init; }
    public required bool VerificationRequired { get; init; }
}
