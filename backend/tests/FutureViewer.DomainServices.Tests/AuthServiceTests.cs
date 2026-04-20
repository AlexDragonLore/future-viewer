using FluentAssertions;
using FutureViewer.Domain.Entities;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Exceptions;
using FutureViewer.DomainServices.Interfaces;
using FutureViewer.DomainServices.Services;
using Moq;

namespace FutureViewer.DomainServices.Tests;

public sealed class AuthServiceTests
{
    private static (AuthService sut, Mock<IUserRepository> users, Mock<IPasswordHasher> hasher, Mock<IJwtTokenService> jwt, Mock<IEmailSender> email, Mock<IEmailLinkBuilder> links) CreateSut()
    {
        var users = new Mock<IUserRepository>();
        var hasher = new Mock<IPasswordHasher>();
        var jwt = new Mock<IJwtTokenService>();
        var email = new Mock<IEmailSender>();
        var links = new Mock<IEmailLinkBuilder>();
        links.Setup(l => l.BuildVerificationLink(It.IsAny<string>())).Returns("http://link");
        links.Setup(l => l.BuildPasswordResetLink(It.IsAny<string>())).Returns("http://reset-link");
        var sut = new AuthService(users.Object, hasher.Object, jwt.Object, email.Object, links.Object);
        return (sut, users, hasher, jwt, email, links);
    }

    [Fact]
    public async Task Register_creates_unverified_user_and_sends_email()
    {
        var (sut, users, hasher, _, email, _) = CreateSut();
        users.Setup(u => u.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        users.Setup(u => u.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User u, CancellationToken _) => u);
        hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed");

        var result = await sut.RegisterAsync(new RegisterRequest { Email = "Test@Example.com", Password = "password123" });

        result.Email.Should().Be("test@example.com");
        result.VerificationRequired.Should().BeTrue();
        users.Verify(u => u.AddAsync(It.Is<User>(x =>
            x.Email == "test@example.com"
            && x.PasswordHash == "hashed"
            && !x.IsEmailVerified
            && x.EmailVerificationToken != null
            && x.EmailVerificationSentAt != null), It.IsAny<CancellationToken>()), Times.Once);
        email.Verify(e => e.SendAsync("test@example.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Register_throws_conflict_when_email_taken()
    {
        var (sut, users, _, _, _, _) = CreateSut();
        users.Setup(u => u.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Email = "a@b.c", PasswordHash = "x" });

        var act = () => sut.RegisterAsync(new RegisterRequest { Email = "a@b.c", Password = "password123" });

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task VerifyEmail_sets_verified_and_returns_jwt()
    {
        var (sut, users, _, jwt, _, _) = CreateSut();
        var user = new User
        {
            Email = "a@b.c",
            PasswordHash = "h",
            EmailVerificationToken = "tok",
            EmailVerificationSentAt = DateTime.UtcNow
        };
        users.Setup(u => u.GetByEmailVerificationTokenAsync("tok", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        jwt.Setup(j => j.CreateAccessToken(It.IsAny<User>()))
            .Returns(("jwt", DateTime.UtcNow.AddHours(1)));

        var result = await sut.VerifyEmailAsync("tok");

        result.AccessToken.Should().Be("jwt");
        user.IsEmailVerified.Should().BeTrue();
        user.EmailVerificationToken.Should().BeNull();
        user.EmailVerificationSentAt.Should().BeNull();
        users.Verify(u => u.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task VerifyEmail_throws_when_token_expired()
    {
        var (sut, users, _, _, _, _) = CreateSut();
        var user = new User
        {
            Email = "a@b.c",
            PasswordHash = "h",
            EmailVerificationToken = "tok",
            EmailVerificationSentAt = DateTime.UtcNow.AddDays(-2)
        };
        users.Setup(u => u.GetByEmailVerificationTokenAsync("tok", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var act = () => sut.VerifyEmailAsync("tok");

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task VerifyEmail_throws_not_found_for_unknown_token()
    {
        var (sut, users, _, _, _, _) = CreateSut();
        users.Setup(u => u.GetByEmailVerificationTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var act = () => sut.VerifyEmailAsync("missing");

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Login_throws_EmailNotVerified_when_not_verified()
    {
        var (sut, users, hasher, _, _, _) = CreateSut();
        users.Setup(u => u.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Email = "a@b.c", PasswordHash = "hash", IsEmailVerified = false });
        hasher.Setup(h => h.Verify("pw", "hash")).Returns(true);

        var act = () => sut.LoginAsync(new LoginRequest { Email = "a@b.c", Password = "pw" });

        await act.Should().ThrowAsync<EmailNotVerifiedException>();
    }

    [Fact]
    public async Task Login_succeeds_when_verified()
    {
        var (sut, users, hasher, jwt, _, _) = CreateSut();
        users.Setup(u => u.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Email = "admin@b.c", PasswordHash = "hash", IsAdmin = true, IsEmailVerified = true });
        hasher.Setup(h => h.Verify("pw", "hash")).Returns(true);
        jwt.Setup(j => j.CreateAccessToken(It.IsAny<User>())).Returns(("tok", DateTime.UtcNow.AddHours(1)));

        var result = await sut.LoginAsync(new LoginRequest { Email = "admin@b.c", Password = "pw" });

        result.IsAdmin.Should().BeTrue();
        result.AccessToken.Should().Be("tok");
    }

    [Fact]
    public async Task Login_throws_when_password_invalid()
    {
        var (sut, users, hasher, _, _, _) = CreateSut();
        users.Setup(u => u.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Email = "a@b.c", PasswordHash = "hash", IsEmailVerified = true });
        hasher.Setup(h => h.Verify("pw", "hash")).Returns(false);

        var act = () => sut.LoginAsync(new LoginRequest { Email = "a@b.c", Password = "pw" });

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task ResendVerification_is_noop_for_unknown_email()
    {
        var (sut, users, _, _, email, _) = CreateSut();
        users.Setup(u => u.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        await sut.ResendVerificationAsync(new ResendVerificationRequest { Email = "missing@x.com" });

        email.Verify(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResendVerification_throttles_within_60_seconds()
    {
        var (sut, users, _, _, _, _) = CreateSut();
        users.Setup(u => u.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                Email = "a@b.c",
                PasswordHash = "h",
                IsEmailVerified = false,
                EmailVerificationSentAt = DateTime.UtcNow.AddSeconds(-10),
                EmailVerificationToken = "t"
            });

        var act = () => sut.ResendVerificationAsync(new ResendVerificationRequest { Email = "a@b.c" });

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task ResendVerification_sends_when_old_enough()
    {
        var (sut, users, _, _, email, _) = CreateSut();
        users.Setup(u => u.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                Email = "a@b.c",
                PasswordHash = "h",
                IsEmailVerified = false,
                EmailVerificationSentAt = DateTime.UtcNow.AddMinutes(-5),
                EmailVerificationToken = "old"
            });

        await sut.ResendVerificationAsync(new ResendVerificationRequest { Email = "a@b.c" });

        email.Verify(e => e.SendAsync("a@b.c", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ForgotPassword_is_silent_for_unknown_email()
    {
        var (sut, users, _, _, email, _) = CreateSut();
        users.Setup(u => u.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        await sut.ForgotPasswordAsync(new ForgotPasswordRequest { Email = "missing@x.com" });

        email.Verify(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ForgotPassword_saves_token_and_sends_email()
    {
        var (sut, users, _, _, email, _) = CreateSut();
        var user = new User { Email = "a@b.c", PasswordHash = "h", IsEmailVerified = true };
        users.Setup(u => u.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        await sut.ForgotPasswordAsync(new ForgotPasswordRequest { Email = "A@B.C" });

        user.PasswordResetToken.Should().NotBeNullOrWhiteSpace();
        user.PasswordResetTokenExpiresAt.Should().NotBeNull();
        user.PasswordResetTokenExpiresAt!.Value.Should().BeAfter(DateTime.UtcNow);
        users.Verify(u => u.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        email.Verify(e => e.SendAsync("a@b.c", It.Is<string>(s => s.Contains("пароля")), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResetPassword_updates_hash_clears_token_and_returns_jwt()
    {
        var (sut, users, hasher, jwt, _, _) = CreateSut();
        var user = new User
        {
            Email = "a@b.c",
            PasswordHash = "old-hash",
            IsEmailVerified = true,
            PasswordResetToken = "tok",
            PasswordResetTokenExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };
        users.Setup(u => u.GetByPasswordResetTokenAsync("tok", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        hasher.Setup(h => h.Hash("new-password")).Returns("new-hash");
        jwt.Setup(j => j.CreateAccessToken(It.IsAny<User>())).Returns(("jwt", DateTime.UtcNow.AddHours(1)));

        var result = await sut.ResetPasswordAsync(new ResetPasswordRequest { Token = "tok", NewPassword = "new-password" });

        result.AccessToken.Should().Be("jwt");
        user.PasswordHash.Should().Be("new-hash");
        user.PasswordResetToken.Should().BeNull();
        user.PasswordResetTokenExpiresAt.Should().BeNull();
        users.Verify(u => u.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResetPassword_throws_when_token_expired()
    {
        var (sut, users, _, _, _, _) = CreateSut();
        var user = new User
        {
            Email = "a@b.c",
            PasswordHash = "h",
            PasswordResetToken = "tok",
            PasswordResetTokenExpiresAt = DateTime.UtcNow.AddMinutes(-1)
        };
        users.Setup(u => u.GetByPasswordResetTokenAsync("tok", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var act = () => sut.ResetPasswordAsync(new ResetPasswordRequest { Token = "tok", NewPassword = "new-password" });

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task ResetPassword_throws_not_found_for_unknown_token()
    {
        var (sut, users, _, _, _, _) = CreateSut();
        users.Setup(u => u.GetByPasswordResetTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var act = () => sut.ResetPasswordAsync(new ResetPasswordRequest { Token = "missing", NewPassword = "new-password" });

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
