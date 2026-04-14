namespace FutureViewer.DomainServices.DTOs;

public sealed record AuthResponse(string AccessToken, DateTime ExpiresAt, Guid UserId, string Email);
