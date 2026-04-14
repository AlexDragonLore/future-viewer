namespace FutureViewer.DomainServices.DTOs;

public sealed class AuthResponse
{
    public required string AccessToken { get; init; }
    public required DateTime ExpiresAt { get; init; }
    public required Guid UserId { get; init; }
    public required string Email { get; init; }
}
