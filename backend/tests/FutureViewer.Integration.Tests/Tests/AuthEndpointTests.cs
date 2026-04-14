using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.Integration.Tests.Fixtures;

namespace FutureViewer.Integration.Tests.Tests;

public class AuthEndpointTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public AuthEndpointTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Register_and_login_returns_jwt()
    {
        var client = _fixture.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@example.com";

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "password123"));
        registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "password123"));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        auth!.AccessToken.Should().NotBeNullOrWhiteSpace();
        auth.Email.Should().Be(email);
    }

    [Fact]
    public async Task Register_duplicate_email_returns_conflict()
    {
        var client = _fixture.CreateClient();
        var email = $"dup-{Guid.NewGuid():N}@example.com";

        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "password123"));
        var second = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "password123"));

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Login_wrong_password_returns_unauthorized()
    {
        var client = _fixture.CreateClient();
        var email = $"wrong-{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "password123"));

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "wrongpw9"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
