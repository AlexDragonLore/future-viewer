using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Interfaces;
using FutureViewer.Integration.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class AuthEndpointTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public AuthEndpointTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Register_returns_accepted_without_token_and_sends_email()
    {
        var client = _fixture.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@example.com";

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Email = email, Password = "password123" });
        registerResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var body = await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>();
        body!.VerificationRequired.Should().BeTrue();
        body.Email.Should().Be(email);

        var captured = _fixture.EmailSender.LastFor(email);
        captured.Should().NotBeNull();
        captured!.Subject.Should().Contain("Подтверждение");
    }

    [Fact]
    public async Task Login_before_verification_returns_forbidden()
    {
        var client = _fixture.CreateClient();
        var email = $"unv-{Guid.NewGuid():N}@example.com";

        await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Email = email, Password = "password123" });

        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = email, Password = "password123" });

        login.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Verify_email_with_valid_token_allows_login()
    {
        var client = _fixture.CreateClient();
        var email = $"verify-{Guid.NewGuid():N}@example.com";

        await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Email = email, Password = "password123" });

        string token;
        using (var scope = _fixture.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var user = await users.GetByEmailAsync(email.ToLowerInvariant());
            token = user!.EmailVerificationToken!;
        }

        var verifyResponse = await client.PostAsJsonAsync("/api/auth/verify-email",
            new VerifyEmailRequest { Token = token });
        verifyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await verifyResponse.Content.ReadFromJsonAsync<AuthResponse>();
        auth!.AccessToken.Should().NotBeNullOrWhiteSpace();

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = email, Password = "password123" });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Verify_email_with_unknown_token_returns_not_found()
    {
        var client = _fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/verify-email",
            new VerifyEmailRequest { Token = "nonexistent-token" });
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Resend_verification_sends_new_email_for_unverified_user()
    {
        var client = _fixture.CreateClient();
        var email = $"resend-{Guid.NewGuid():N}@example.com";

        await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Email = email, Password = "password123" });

        using (var scope = _fixture.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var user = await users.GetByEmailAsync(email.ToLowerInvariant());
            user!.EmailVerificationSentAt = DateTime.UtcNow.AddMinutes(-5);
            await users.UpdateAsync(user);
        }

        var before = _fixture.EmailSender.Sent.Count(e => e.To == email);

        var response = await client.PostAsJsonAsync("/api/auth/resend-verification",
            new ResendVerificationRequest { Email = email });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = _fixture.EmailSender.Sent.Count(e => e.To == email);
        after.Should().Be(before + 1);
    }

    [Fact]
    public async Task Register_duplicate_email_returns_conflict()
    {
        var client = _fixture.CreateClient();
        var email = $"dup-{Guid.NewGuid():N}@example.com";

        await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Email = email, Password = "password123" });
        var second = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Email = email, Password = "password123" });

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Login_wrong_password_returns_unauthorized()
    {
        var client = _fixture.CreateClient();
        var email = $"wrong-{Guid.NewGuid():N}@example.com";
        await _fixture.RegisterAndLoginAsync(client, email, "password123");

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = email, Password = "wrongpw9" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ForgotPassword_then_reset_allows_login_with_new_password()
    {
        var client = _fixture.CreateClient();
        var email = $"reset-{Guid.NewGuid():N}@example.com";
        await _fixture.RegisterAndLoginAsync(client, email, "oldpassword1");

        var forgot = await client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest { Email = email });
        forgot.StatusCode.Should().Be(HttpStatusCode.NoContent);

        string token;
        using (var scope = _fixture.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var user = await users.GetByEmailAsync(email.ToLowerInvariant());
            token = user!.PasswordResetToken!;
            token.Should().NotBeNullOrWhiteSpace();
        }

        var resetEmail = _fixture.EmailSender.LastFor(email);
        resetEmail.Should().NotBeNull();
        resetEmail!.Subject.Should().Contain("Восстановление");

        var reset = await client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest { Token = token, NewPassword = "newpassword1" });
        reset.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await reset.Content.ReadFromJsonAsync<AuthResponse>();
        auth!.AccessToken.Should().NotBeNullOrWhiteSpace();

        var loginOld = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = email, Password = "oldpassword1" });
        loginOld.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var loginNew = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = email, Password = "newpassword1" });
        loginNew.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ForgotPassword_returns_no_content_for_unknown_email()
    {
        var client = _fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest { Email = $"nobody-{Guid.NewGuid():N}@example.com" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ResetPassword_with_unknown_token_returns_not_found()
    {
        var client = _fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest { Token = "nonexistent", NewPassword = "password123" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ResetPassword_with_expired_token_returns_unauthorized()
    {
        var client = _fixture.CreateClient();
        var email = $"expired-{Guid.NewGuid():N}@example.com";
        await _fixture.RegisterAndLoginAsync(client, email, "password123");

        await client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest { Email = email });

        string token;
        using (var scope = _fixture.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var user = await users.GetByEmailAsync(email.ToLowerInvariant());
            token = user!.PasswordResetToken!;
            user.PasswordResetTokenExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await users.UpdateAsync(user);
        }

        var reset = await client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest { Token = token, NewPassword = "newpassword1" });

        reset.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
