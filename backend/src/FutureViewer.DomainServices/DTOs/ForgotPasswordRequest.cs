namespace FutureViewer.DomainServices.DTOs;

public sealed class ForgotPasswordRequest
{
    public required string Email { get; init; }
}

public sealed class ResetPasswordRequest
{
    public required string Token { get; init; }
    public required string NewPassword { get; init; }
}
