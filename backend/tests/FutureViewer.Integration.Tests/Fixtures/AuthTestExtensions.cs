using System.Net.Http.Json;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace FutureViewer.Integration.Tests.Fixtures;

public static class AuthTestExtensions
{
    public static async Task<AuthResponse> RegisterAndLoginAsync(
        this IntegrationTestFixture fixture,
        HttpClient client,
        string email,
        string password)
    {
        var register = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Email = email, Password = password });
        register.EnsureSuccessStatusCode();

        using (var scope = fixture.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var user = await users.GetByEmailAsync(email.Trim().ToLowerInvariant());
            if (user is null) throw new InvalidOperationException($"User not found after register: {email}");
            user.IsEmailVerified = true;
            user.EmailVerificationToken = null;
            user.EmailVerificationSentAt = null;
            await users.UpdateAsync(user);
        }

        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = email, Password = password });
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        return auth ?? throw new InvalidOperationException("Login returned null");
    }
}
