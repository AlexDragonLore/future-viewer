using System.Security.Cryptography;
using System.Text;
using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Exceptions;
using FutureViewer.DomainServices.Interfaces;

namespace FutureViewer.DomainServices.Services;

public sealed class AuthService
{
    private static readonly TimeSpan VerificationTokenLifetime = TimeSpan.FromHours(24);
    private static readonly TimeSpan ResendThrottle = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan PasswordResetTokenLifetime = TimeSpan.FromHours(1);

    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _jwt;
    private readonly IEmailSender _email;
    private readonly IEmailLinkBuilder _links;
    private readonly PrivacyService? _privacy;
    private readonly IUnitOfWork? _unitOfWork;

    public AuthService(
        IUserRepository users,
        IPasswordHasher hasher,
        IJwtTokenService jwt,
        IEmailSender email,
        IEmailLinkBuilder links,
        PrivacyService? privacy = null,
        IUnitOfWork? unitOfWork = null)
    {
        _users = users;
        _hasher = hasher;
        _jwt = jwt;
        _email = email;
        _links = links;
        _privacy = privacy;
        _unitOfWork = unitOfWork;
    }

    public async Task<RegisterResponse> RegisterAsync(
        RegisterRequest request,
        string? ipAddress = null,
        string? userAgent = null,
        string? correlationId = null,
        CancellationToken ct = default)
    {
        var normalized = request.Email.Trim().ToLowerInvariant();
        EnsureEmailDeliveryAvailable();
        var existing = await _users.GetByEmailAsync(normalized, ct);
        if (existing is not null)
        {
            // Keep registration non-enumerable: do not reveal whether the account
            // exists, is verified, or is pending deletion.
            return new RegisterResponse
            {
                UserId = Guid.Empty,
                Email = normalized,
                VerificationRequired = true
            };
        }

        var token = GenerateToken();
        var now = DateTime.UtcNow;
        var user = new User
        {
            Email = normalized,
            PasswordHash = _hasher.Hash(request.Password),
            CreatedAt = now,
            IsEmailVerified = false,
            EmailVerificationToken = HashToken(token),
            EmailVerificationSentAt = now
        };
        if (_privacy is not null && _unitOfWork is not null)
        {
            await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
            {
                await _users.AddAsync(user, innerCt);
                await _privacy.RecordRegistrationAsync(
                    user,
                    request,
                    ipAddress,
                    userAgent,
                    correlationId ?? Guid.NewGuid().ToString("N"),
                    innerCt);
                return true;
            }, ct);
        }
        else
        {
            await _users.AddAsync(user, ct);
        }

        await SendVerificationEmailAsync(normalized, token, ct);

        return new RegisterResponse
        {
            // Do not reveal whether this request created a new account. Account
            // identity is returned only after email verification/login.
            UserId = Guid.Empty,
            Email = user.Email,
            VerificationRequired = true
        };
    }

    public async Task<AuthResponse> VerifyEmailAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new NotFoundException("Invalid verification token");

        var user = await _users.GetByEmailVerificationTokenAsync(HashToken(token), ct)
            ?? throw new NotFoundException("Invalid verification token");

        if (user.AccountStatus != UserAccountStatus.Active)
            throw new UnauthorizedException("Invalid verification token");

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
        EnsureEmailDeliveryAvailable();
        var normalized = request.Email.Trim().ToLowerInvariant();
        var user = await _users.GetByEmailAsync(normalized, ct);
        if (user is null || user.IsEmailVerified || user.AccountStatus != UserAccountStatus.Active)
            return;

        if (user.EmailVerificationSentAt is not null
            && DateTime.UtcNow - user.EmailVerificationSentAt.Value < ResendThrottle)
            return;

        var token = GenerateToken();
        user.EmailVerificationToken = HashToken(token);
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

        if (user.AccountStatus != UserAccountStatus.Active)
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

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default)
    {
        EnsureEmailDeliveryAvailable();
        var normalized = request.Email.Trim().ToLowerInvariant();
        var user = await _users.GetByEmailAsync(normalized, ct);
        if (user is null || user.AccountStatus != UserAccountStatus.Active)
            return;

        if (user.PasswordResetTokenExpiresAt is not null
            && user.PasswordResetTokenExpiresAt.Value - DateTime.UtcNow > PasswordResetTokenLifetime - ResendThrottle)
            return;

        var token = GenerateToken();
        user.PasswordResetToken = HashToken(token);
        user.PasswordResetTokenExpiresAt = DateTime.UtcNow.Add(PasswordResetTokenLifetime);
        await _users.UpdateAsync(user, ct);

        await SendPasswordResetEmailAsync(normalized, token, ct);
    }

    public async Task<AuthResponse> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            throw new NotFoundException("Invalid password reset token");

        var user = await _users.GetByPasswordResetTokenAsync(HashToken(request.Token), ct)
            ?? throw new NotFoundException("Invalid password reset token");

        if (user.AccountStatus != UserAccountStatus.Active)
            throw new UnauthorizedException("Invalid password reset token");

        if (user.PasswordResetTokenExpiresAt is null
            || user.PasswordResetTokenExpiresAt.Value < DateTime.UtcNow)
            throw new UnauthorizedException("Password reset token has expired");

        user.PasswordHash = _hasher.Hash(request.NewPassword);
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiresAt = null;
        user.IsEmailVerified = true;
        user.EmailVerificationToken = null;
        user.EmailVerificationSentAt = null;
        user.SecurityVersion++;
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

    private void EnsureEmailDeliveryAvailable()
    {
        if (!_email.IsConfigured)
            throw new FeatureDisabledException(
                "email_verification",
                "Подтверждение почты временно недоступно. Повторите позже или обратитесь в поддержку.");
    }

    private async Task SendVerificationEmailAsync(string email, string token, CancellationToken ct)
    {
        var link = _links.BuildVerificationLink(token);
        var html = AuthEmailTemplate.Verification(link);
        await _email.SendAsync(email, "Подтверждение регистрации — Вуаль Грядущего", html, ct);
    }

    private async Task SendPasswordResetEmailAsync(string email, string token, CancellationToken ct)
    {
        var link = _links.BuildPasswordResetLink(token);
        var html = AuthEmailTemplate.PasswordReset(link);
        await _email.SendAsync(email, "Восстановление пароля — Вуаль Грядущего", html, ct);
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

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
