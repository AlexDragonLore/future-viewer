using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.Interfaces;
using FutureViewer.Infrastructure.Auth;
using FutureViewer.Integration.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class JwtSessionEndpointTests(IntegrationTestFixture fixture) : IClassFixture<IntegrationTestFixture>
{
    [Theory]
    [InlineData(179, HttpStatusCode.OK)]
    [InlineData(181, HttpStatusCode.Unauthorized)]
    public async Task Session_stays_valid_for_180_days_then_expires(int ageDays, HttpStatusCode expected)
    {
        var client = fixture.CreateClient();
        var auth = await fixture.RegisterAndLoginAsync(client, $"session-{Guid.NewGuid():N}@example.com", "password123");
        using var scope = fixture.Services.CreateScope();
        var user = (await scope.ServiceProvider.GetRequiredService<IUserRepository>().GetByIdAsync(auth.UserId))!;
        var options = scope.ServiceProvider.GetRequiredService<IOptions<JwtOptions>>().Value;
        var issuedAt = DateTime.UtcNow.AddDays(-ageDays);
        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: [new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()), new Claim("security_version", user.SecurityVersion.ToString())],
            notBefore: issuedAt,
            expires: issuedAt.AddDays(180),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Secret)), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));

        (await client.GetAsync("/api/subscription/status")).StatusCode.Should().Be(expected);
    }

    [Theory]
    [InlineData("security_version")]
    [InlineData("blocked")]
    [InlineData("admin_revoked")]
    public async Task Session_is_revoked_immediately_when_security_state_changes(string change)
    {
        var client = fixture.CreateClient();
        var auth = await fixture.RegisterAndLoginAsync(client, $"revoke-{Guid.NewGuid():N}@example.com", "password123");
        using var scope = fixture.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var user = (await users.GetByIdAsync(auth.UserId))!;
        user.IsAdmin = change == "admin_revoked";
        await users.UpdateAsync(user);
        var (token, expires) = scope.ServiceProvider.GetRequiredService<IJwtTokenService>().CreateAccessToken(user);
        expires.Should().BeCloseTo(DateTime.UtcNow.AddDays(180), TimeSpan.FromSeconds(10));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        (await client.GetAsync("/api/subscription/status")).StatusCode.Should().Be(HttpStatusCode.OK);

        switch (change)
        {
            case "security_version": user.SecurityVersion++; break;
            case "blocked": user.AccountStatus = UserAccountStatus.Blocked; break;
            case "admin_revoked": user.IsAdmin = false; break;
        }
        await users.UpdateAsync(user);

        (await client.GetAsync("/api/subscription/status")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
