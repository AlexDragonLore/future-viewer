using FutureViewer.Domain.Entities;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Exceptions;
using FutureViewer.DomainServices.Interfaces;

namespace FutureViewer.DomainServices.Services;

public sealed class AuthService
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _jwt;

    public AuthService(IUserRepository users, IPasswordHasher hasher, IJwtTokenService jwt)
    {
        _users = users;
        _hasher = hasher;
        _jwt = jwt;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var normalized = request.Email.Trim().ToLowerInvariant();
        var existing = await _users.GetByEmailAsync(normalized, ct);
        if (existing is not null)
            throw new ConflictException("User with this email already exists");

        var user = new User
        {
            Email = normalized,
            PasswordHash = _hasher.Hash(request.Password),
            CreatedAt = DateTime.UtcNow
        };
        await _users.AddAsync(user, ct);

        var (token, expires) = _jwt.CreateAccessToken(user);
        return new AuthResponse
        {
            AccessToken = token,
            ExpiresAt = expires,
            UserId = user.Id,
            Email = user.Email
        };
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var normalized = request.Email.Trim().ToLowerInvariant();
        var user = await _users.GetByEmailAsync(normalized, ct)
            ?? throw new UnauthorizedException("Invalid credentials");

        if (!_hasher.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedException("Invalid credentials");

        var (token, expires) = _jwt.CreateAccessToken(user);
        return new AuthResponse
        {
            AccessToken = token,
            ExpiresAt = expires,
            UserId = user.Id,
            Email = user.Email
        };
    }
}
