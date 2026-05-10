namespace FutureViewer.DomainServices.DTOs;

public sealed class ResendVerificationRequest
{
    public required string Email { get; init; }
}

public sealed class VerifyEmailRequest
{
    public required string Token { get; init; }
}
