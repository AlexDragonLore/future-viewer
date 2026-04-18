using FluentAssertions;
using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.Exceptions;
using FutureViewer.DomainServices.Interfaces;
using FutureViewer.DomainServices.Services;
using Moq;

namespace FutureViewer.DomainServices.Tests;

public sealed class SubscriptionServiceTests
{
    private static Mock<IUserRepository> UserRepoFor(User user)
    {
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        return users;
    }

    private static Mock<IReadingRepository> ReadingRepoWithCount(int count)
    {
        var readings = new Mock<IReadingRepository>();
        readings.Setup(r => r.CountTodayByUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(count);
        return readings;
    }

    private static User NewUser(SubscriptionStatus status = SubscriptionStatus.None, DateTime? expires = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            Email = "a@b.c",
            PasswordHash = "x",
            SubscriptionStatus = status,
            SubscriptionExpiresAt = expires
        };

    [Fact]
    public async Task EnsureReadingAllowed_allows_active_subscriber_for_any_spread()
    {
        var user = NewUser(SubscriptionStatus.Active, DateTime.UtcNow.AddDays(3));
        var sut = new SubscriptionService(UserRepoFor(user).Object, ReadingRepoWithCount(99).Object);

        await sut.Invoking(s => s.EnsureReadingAllowedAsync(user.Id, SpreadType.CelticCross))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureReadingAllowed_allows_free_single_card_under_limit()
    {
        var user = NewUser();
        var sut = new SubscriptionService(UserRepoFor(user).Object, ReadingRepoWithCount(0).Object);

        await sut.Invoking(s => s.EnsureReadingAllowedAsync(user.Id, SpreadType.SingleCard))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureReadingAllowed_throws_quota_when_single_card_limit_reached()
    {
        var user = NewUser();
        var sut = new SubscriptionService(UserRepoFor(user).Object, ReadingRepoWithCount(1).Object);

        await sut.Invoking(s => s.EnsureReadingAllowedAsync(user.Id, SpreadType.SingleCard))
            .Should().ThrowAsync<QuotaExceededException>();
    }

    [Fact]
    public async Task EnsureReadingAllowed_throws_subscription_required_for_non_single_card_free_user()
    {
        var user = NewUser();
        var sut = new SubscriptionService(UserRepoFor(user).Object, ReadingRepoWithCount(0).Object);

        await sut.Invoking(s => s.EnsureReadingAllowedAsync(user.Id, SpreadType.ThreeCard))
            .Should().ThrowAsync<SubscriptionRequiredException>();
    }

    [Fact]
    public async Task EnsureReadingAllowed_treats_expired_subscription_as_inactive()
    {
        var user = NewUser(SubscriptionStatus.Active, DateTime.UtcNow.AddDays(-1));
        var sut = new SubscriptionService(UserRepoFor(user).Object, ReadingRepoWithCount(0).Object);

        await sut.Invoking(s => s.EnsureReadingAllowedAsync(user.Id, SpreadType.ThreeCard))
            .Should().ThrowAsync<SubscriptionRequiredException>();
    }

    [Fact]
    public async Task EnsureReadingAllowed_throws_unauthorized_when_user_missing()
    {
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var sut = new SubscriptionService(users.Object, ReadingRepoWithCount(0).Object);

        await sut.Invoking(s => s.EnsureReadingAllowedAsync(Guid.NewGuid(), SpreadType.SingleCard))
            .Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task GetStatus_reports_active_subscription()
    {
        var expires = DateTime.UtcNow.AddDays(10);
        var user = NewUser(SubscriptionStatus.Active, expires);
        var sut = new SubscriptionService(UserRepoFor(user).Object, ReadingRepoWithCount(5).Object);

        var status = await sut.GetStatusAsync(user.Id);

        status.IsActive.Should().BeTrue();
        status.Status.Should().Be(SubscriptionStatus.Active);
        status.ExpiresAt.Should().Be(expires);
        status.FreeReadingsUsedToday.Should().Be(5);
        status.FreeReadingsDailyLimit.Should().Be(SubscriptionService.FreeDailyLimit);
        status.CanCreateFreeReading.Should().BeTrue();
    }

    [Fact]
    public async Task GetStatus_reports_free_user_at_limit()
    {
        var user = NewUser();
        var sut = new SubscriptionService(UserRepoFor(user).Object, ReadingRepoWithCount(1).Object);

        var status = await sut.GetStatusAsync(user.Id);

        status.IsActive.Should().BeFalse();
        status.CanCreateFreeReading.Should().BeFalse();
        status.FreeReadingsUsedToday.Should().Be(1);
    }
}
