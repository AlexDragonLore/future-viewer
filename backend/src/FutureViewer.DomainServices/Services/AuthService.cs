using System.Security.Cryptography;
using FutureViewer.Domain.Entities;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Exceptions;
using FutureViewer.DomainServices.Interfaces;

namespace FutureViewer.DomainServices.Services;

public sealed class AuthService
{
    private static readonly TimeSpan VerificationTokenLifetime = TimeSpan.FromHours(24);
    private static readonly TimeSpan ResendThrottle = TimeSpan.FromSeconds(60);

    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _jwt;
    private readonly IEmailSender _email;
    private readonly IEmailLinkBuilder _links;

    public AuthService(
        IUserRepository users,
        IPasswordHasher hasher,
        IJwtTokenService jwt,
        IEmailSender email,
        IEmailLinkBuilder links)
    {
        _users = users;
        _hasher = hasher;
        _jwt = jwt;
        _email = email;
        _links = links;
    }

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var normalized = request.Email.Trim().ToLowerInvariant();
        var existing = await _users.GetByEmailAsync(normalized, ct);
        if (existing is not null)
            throw new ConflictException("User with this email already exists");

        var token = GenerateToken();
        var user = new User
        {
            Email = normalized,
            PasswordHash = _hasher.Hash(request.Password),
            CreatedAt = DateTime.UtcNow,
            IsEmailVerified = false,
            EmailVerificationToken = token,
            EmailVerificationSentAt = DateTime.UtcNow
        };
        await _users.AddAsync(user, ct);

        await SendVerificationEmailAsync(normalized, token, ct);

        return new RegisterResponse
        {
            UserId = user.Id,
            Email = user.Email,
            VerificationRequired = true
        };
    }

    public async Task<AuthResponse> VerifyEmailAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new NotFoundException("Invalid verification token");

        var user = await _users.GetByEmailVerificationTokenAsync(token, ct)
            ?? throw new NotFoundException("Invalid verification token");

        if (user.EmailVerificationSentAt is null
            || DateTime.UtcNow - user.EmailVerificationSentAt.Value > VerificationTokenLifetime)
            throw new UnauthorizedException("Verification token has expired");

        user.IsEmailVerified = true;
        user.EmailVerificationToken = null;
        user.EmailVerificationSentAt = null;
        await _users.UpdateAsync(user, ct);

        var (jwt, expires) = _jwt.CreateAccessToken(user);
        return new AuthResponse
        {
            AccessToken = jwt,
            ExpiresAt = expires,
            UserId = user.Id,
            Email = user.Email,
            IsAdmin = user.IsAdmin
        };
    }

    public async Task ResendVerificationAsync(ResendVerificationRequest request, CancellationToken ct = default)
    {
        var normalized = request.Email.Trim().ToLowerInvariant();
        var user = await _users.GetByEmailAsync(normalized, ct);
        if (user is null || user.IsEmailVerified)
            return;

        if (user.EmailVerificationSentAt is not null
            && DateTime.UtcNow - user.EmailVerificationSentAt.Value < ResendThrottle)
            throw new ConflictException("Verification email was recently sent; please wait before requesting another");

        var token = GenerateToken();
        user.EmailVerificationToken = token;
        user.EmailVerificationSentAt = DateTime.UtcNow;
        await _users.UpdateAsync(user, ct);

        await SendVerificationEmailAsync(normalized, token, ct);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var normalized = request.Email.Trim().ToLowerInvariant();
        var user = await _users.GetByEmailAsync(normalized, ct)
            ?? throw new UnauthorizedException("Invalid credentials");

        if (!_hasher.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedException("Invalid credentials");

        if (!user.IsEmailVerified)
            throw new EmailNotVerifiedException("Email address is not verified");

        var (token, expires) = _jwt.CreateAccessToken(user);
        return new AuthResponse
        {
            AccessToken = token,
            ExpiresAt = expires,
            UserId = user.Id,
            Email = user.Email,
            IsAdmin = user.IsAdmin
        };
    }

    private async Task SendVerificationEmailAsync(string email, string token, CancellationToken ct)
    {
        var link = _links.BuildVerificationLink(token);
        var html = $"""
            <p>Здравствуйте!</p>
            <p>Вы зарегистрировались в «Вуаль Грядущего». Пожалуйста, подтвердите свой email, перейдя по ссылке:</p>
            <p><a href="{link}">{link}</a></p>
            <p>Ссылка действительна 24 часа.</p>
            """;
        await _email.SendAsync(email, "Подтверждение регистрации", html, ct);
    }

    private static string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
