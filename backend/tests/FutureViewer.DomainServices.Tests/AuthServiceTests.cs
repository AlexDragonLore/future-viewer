using FluentAssertions;
using FutureViewer.Domain.Entities;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Exceptions;
using FutureViewer.DomainServices.Interfaces;
using FutureViewer.DomainServices.Services;
using Moq;

namespace FutureViewer.DomainServices.Tests;

public class AuthServiceTests
{
    [Fact]
    public async Task Register_creates_new_user_and_returns_token()
    {
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        users.Setup(u => u.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User u, CancellationToken _) => u);

        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed");

        var jwt = new Mock<IJwtTokenService>();
        jwt.Setup(j => j.CreateAccessToken(It.IsAny<User>()))
            .Returns(("tok", DateTime.UtcNow.AddHours(1)));

        var sut = new AuthService(users.Object, hasher.Object, jwt.Object);

        var result = await sut.RegisterAsync(new RegisterRequest("Test@Example.com", "password123"));

        result.AccessToken.Should().Be("tok");
        result.Email.Should().Be("test@example.com");
        users.Verify(u => u.AddAsync(It.Is<User>(x => x.Email == "test@example.com" && x.PasswordHash == "hashed"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Register_throws_conflict_when_email_taken()
    {
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Email = "a@b.c" });

        var sut = new AuthService(users.Object, Mock.Of<IPasswordHasher>(), Mock.Of<IJwtTokenService>());

        var act = () => sut.RegisterAsync(new RegisterRequest("a@b.c", "password123"));

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Login_throws_when_password_invalid()
    {
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Email = "a@b.c", PasswordHash = "hash" });

        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Verify("pw", "hash")).Returns(false);

        var sut = new AuthService(users.Object, hasher.Object, Mock.Of<IJwtTokenService>());

        var act = () => sut.LoginAsync(new LoginRequest("a@b.c", "pw"));

        await act.Should().ThrowAsync<UnauthorizedException>();
    }
}
