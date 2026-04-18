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

    private static IPaymentProvider StubPayments() => new Mock<IPaymentProvider>().Object;

    [Fact]
    public async Task EnsureReadingAllowed_allows_active_subscriber_for_any_spread()
    {
        var user = NewUser(SubscriptionStatus.Active, DateTime.UtcNow.AddDays(3));
        var sut = new SubscriptionService(UserRepoFor(user).Object, ReadingRepoWithCount(99).Object, StubPayments());

        await sut.Invoking(s => s.EnsureReadingAllowedAsync(user.Id, SpreadType.CelticCross))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureReadingAllowed_allows_free_single_card_under_limit()
    {
        var user = NewUser();
        var sut = new SubscriptionService(UserRepoFor(user).Object, ReadingRepoWithCount(0).Object, StubPayments());

        await sut.Invoking(s => s.EnsureReadingAllowedAsync(user.Id, SpreadType.SingleCard))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureReadingAllowed_throws_quota_when_single_card_limit_reached()
    {
        var user = NewUser();
        var sut = new SubscriptionService(UserRepoFor(user).Object, ReadingRepoWithCount(1).Object, StubPayments());

        await sut.Invoking(s => s.EnsureReadingAllowedAsync(user.Id, SpreadType.SingleCard))
            .Should().ThrowAsync<QuotaExceededException>();
    }

    [Fact]
    public async Task EnsureReadingAllowed_throws_subscription_required_for_non_single_card_free_user()
    {
        var user = NewUser();
        var sut = new SubscriptionService(UserRepoFor(user).Object, ReadingRepoWithCount(0).Object, StubPayments());

        await sut.Invoking(s => s.EnsureReadingAllowedAsync(user.Id, SpreadType.ThreeCard))
            .Should().ThrowAsync<SubscriptionRequiredException>();
    }

    [Fact]
    public async Task EnsureReadingAllowed_treats_expired_subscription_as_inactive()
    {
        var user = NewUser(SubscriptionStatus.Active, DateTime.UtcNow.AddDays(-1));
        var sut = new SubscriptionService(UserRepoFor(user).Object, ReadingRepoWithCount(0).Object, StubPayments());

        await sut.Invoking(s => s.EnsureReadingAllowedAsync(user.Id, SpreadType.ThreeCard))
            .Should().ThrowAsync<SubscriptionRequiredException>();
    }

    [Fact]
    public async Task EnsureReadingAllowed_throws_unauthorized_when_user_missing()
    {
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var sut = new SubscriptionService(users.Object, ReadingRepoWithCount(0).Object, StubPayments());

        await sut.Invoking(s => s.EnsureReadingAllowedAsync(Guid.NewGuid(), SpreadType.SingleCard))
            .Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task GetStatus_reports_active_subscription()
    {
        var expires = DateTime.UtcNow.AddDays(10);
        var user = NewUser(SubscriptionStatus.Active, expires);
        var sut = new SubscriptionService(UserRepoFor(user).Object, ReadingRepoWithCount(5).Object, StubPayments());

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
        var sut = new SubscriptionService(UserRepoFor(user).Object, ReadingRepoWithCount(1).Object, StubPayments());

        var status = await sut.GetStatusAsync(user.Id);

        status.IsActive.Should().BeFalse();
        status.CanCreateFreeReading.Should().BeFalse();
        status.FreeReadingsUsedToday.Should().Be(1);
    }

    [Fact]
    public async Task CreatePayment_returns_confirmation_url_from_provider()
    {
        var user = NewUser();
        var payments = new Mock<IPaymentProvider>();
        payments.Setup(p => p.CreateSubscriptionPaymentAsync(user.Id, user.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentCreationResult
            {
                PaymentId = "pay-1",
                ConfirmationUrl = "https://yk/confirm",
                Status = "pending"
            });

        var sut = new SubscriptionService(UserRepoFor(user).Object, ReadingRepoWithCount(0).Object, payments.Object);

        var result = await sut.CreatePaymentAsync(user.Id);

        result.PaymentId.Should().Be("pay-1");
        result.ConfirmationUrl.Should().Be("https://yk/confirm");
        result.Status.Should().Be("pending");
    }

    [Fact]
    public async Task ProcessWebhook_ignores_replay_of_same_payment_id()
    {
        var existingExpiry = DateTime.UtcNow.AddDays(10);
        var user = NewUser(SubscriptionStatus.Active, existingExpiry);
        user.YukassaSubscriptionId = "pay-replay";
        var users = UserRepoFor(user);
        var payments = new Mock<IPaymentProvider>();
        payments.Setup(p => p.ParseWebhook(It.IsAny<string>())).Returns(new PaymentWebhookEvent
        {
            Type = PaymentWebhookEventType.PaymentSucceeded,
            PaymentId = "pay-replay",
            UserId = user.Id
        });

        var sut = new SubscriptionService(users.Object, ReadingRepoWithCount(0).Object, payments.Object);

        var handled = await sut.ProcessWebhookAsync("{}");

        handled.Should().BeFalse();
        user.SubscriptionExpiresAt.Should().Be(existingExpiry);
        users.Verify(u => u.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessWebhook_activates_subscription_on_payment_succeeded()
    {
        var user = NewUser();
        var users = UserRepoFor(user);
        var payments = new Mock<IPaymentProvider>();
        payments.Setup(p => p.ParseWebhook(It.IsAny<string>())).Returns(new PaymentWebhookEvent
        {
            Type = PaymentWebhookEventType.PaymentSucceeded,
            PaymentId = "pay-xyz",
            UserId = user.Id
        });

        var sut = new SubscriptionService(users.Object, ReadingRepoWithCount(0).Object, payments.Object);

        var handled = await sut.ProcessWebhookAsync("{}");

        handled.Should().BeTrue();
        user.SubscriptionStatus.Should().Be(SubscriptionStatus.Active);
        user.SubscriptionExpiresAt.Should().NotBeNull();
        user.SubscriptionExpiresAt!.Value.Should().BeAfter(DateTime.UtcNow.AddDays(SubscriptionService.SubscriptionDurationDays - 1));
        user.YukassaSubscriptionId.Should().Be("pay-xyz");
        users.Verify(u => u.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessWebhook_extends_existing_active_subscription()
    {
        var existingExpiry = DateTime.UtcNow.AddDays(5);
        var user = NewUser(SubscriptionStatus.Active, existingExpiry);
        var payments = new Mock<IPaymentProvider>();
        payments.Setup(p => p.ParseWebhook(It.IsAny<string>())).Returns(new PaymentWebhookEvent
        {
            Type = PaymentWebhookEventType.PaymentSucceeded,
            PaymentId = "pay-ext",
            UserId = user.Id
        });

        var sut = new SubscriptionService(UserRepoFor(user).Object, ReadingRepoWithCount(0).Object, payments.Object);

        await sut.ProcessWebhookAsync("{}");

        user.SubscriptionExpiresAt.Should().BeAfter(existingExpiry.AddDays(SubscriptionService.SubscriptionDurationDays - 1));
    }

    [Fact]
    public async Task ProcessWebhook_ignores_cancelled_events()
    {
        var user = NewUser();
        var users = UserRepoFor(user);
        var payments = new Mock<IPaymentProvider>();
        payments.Setup(p => p.ParseWebhook(It.IsAny<string>())).Returns(new PaymentWebhookEvent
        {
            Type = PaymentWebhookEventType.PaymentCanceled,
            PaymentId = "pay-cancel",
            UserId = user.Id
        });

        var sut = new SubscriptionService(users.Object, ReadingRepoWithCount(0).Object, payments.Object);

        var handled = await sut.ProcessWebhookAsync("{}");

        handled.Should().BeFalse();
        user.SubscriptionStatus.Should().Be(SubscriptionStatus.None);
        users.Verify(u => u.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessWebhook_ignores_unparseable_body()
    {
        var user = NewUser();
        var payments = new Mock<IPaymentProvider>();
        payments.Setup(p => p.ParseWebhook(It.IsAny<string>())).Returns((PaymentWebhookEvent?)null);

        var sut = new SubscriptionService(UserRepoFor(user).Object, ReadingRepoWithCount(0).Object, payments.Object);

        var handled = await sut.ProcessWebhookAsync("not-json");

        handled.Should().BeFalse();
    }
}
