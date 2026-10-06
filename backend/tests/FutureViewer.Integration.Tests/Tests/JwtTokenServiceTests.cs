using System.IdentityModel.Tokens.Jwt;
using System.Text;
using FluentAssertions;
using FutureViewer.Domain.Entities;
using FutureViewer.Infrastructure.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class JwtTokenServiceTests
{
    [Fact]
    public void Default_session_is_signed_for_180_days_and_keeps_revocation_claim()
    {
        var options = new JwtOptions { Secret = "test-only-jwt-signing-key-at-least-32-characters" };
        var user = new User { Email = "session@example.com", PasswordHash = "test", SecurityVersion = 3 };

        var (encoded, expires) = new JwtTokenService(Options.Create(options)).CreateAccessToken(user);
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(encoded);
        (token.ValidTo - token.ValidFrom).Should().Be(TimeSpan.FromDays(180));
        expires.Should().BeCloseTo(token.ValidTo, TimeSpan.FromSeconds(1));
        token.Claims.Should().Contain(c => c.Type == "security_version" && c.Value == "3");
        handler.ValidateToken(encoded, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = options.Issuer,
            ValidateAudience = true,
            ValidAudience = options.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Secret)),
            ClockSkew = TimeSpan.Zero
        }, out _);
    }

    [Fact]
    public void Explicit_session_lifetime_configuration_is_honored()
    {
        var options = new JwtOptions
        {
            Secret = "test-only-jwt-signing-key-at-least-32-characters",
            ExpiresMinutes = 90
        };
        var user = new User { Email = "session@example.com", PasswordHash = "test" };

        var (encoded, _) = new JwtTokenService(Options.Create(options)).CreateAccessToken(user);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(encoded);
        (token.ValidTo - token.ValidFrom).Should().Be(TimeSpan.FromMinutes(90));
    }
}
