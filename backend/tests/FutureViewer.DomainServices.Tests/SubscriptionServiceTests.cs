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

    private static Mock<IProcessedPaymentRepository> ProcessedPaymentsAcceptAll()
    {
        var repo = new Mock<IProcessedPaymentRepository>();
        repo.Setup(r => r.TryRecordAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return repo;
    }

    private sealed class PassThroughUnitOfWork : IUnitOfWork
    {
        public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default) =>
            work(ct);
    }

    private static IUnitOfWork Uow() => new PassThroughUnitOfWork();

    [Fact]
    public async Task EnsureReadingAllowed_allows_active_subscriber_for_any_spread()
    {
        var user = NewUser(SubscriptionStatus.Active, DateTime.UtcNow.AddDays(3));
        var sut = new SubscriptionService(UserRepoFor(user).Object, ReadingRepoWithCount(99).Object, StubPayments(), ProcessedPaymentsAcceptAll().Object, Uow());

        await sut.Invoking(s => s.EnsureReadingAllowedAsync(user.Id, SpreadType.CelticCross))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureReadingAllowed_allows_free_single_card_under_limit()
    {
        var user = NewUser();
        var sut = new SubscriptionService(UserRepoFor(user).Object, ReadingRepoWithCount(0).Object, StubPayments(), ProcessedPaymentsAcceptAll().Object, Uow());

        await sut.Invoking(s => s.EnsureReadingAllowedAsync(user.Id, SpreadType.SingleCard))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureReadingAllowed_throws_quota_when_single_card_limit_reached()
    {
        var user = NewUser();
        var sut = new SubscriptionService(UserRepoFor(user).Object, ReadingRepoWithCount(1).Object, StubPayments(), ProcessedPaymentsAcceptAll().Object, Uow());

        await sut.Invoking(s => s.EnsureReadingAllowedAsync(user.Id, SpreadType.SingleCard))
            .Should().ThrowAsync<QuotaExceededException>();
    }

    [Fact]
    public async Task EnsureReadingAllowed_throws_subscription_required_for_non_single_card_free_user()
    {
        var user = NewUser();
        var sut = new SubscriptionService(UserRepoFor(user).Object, ReadingRepoWithCount(0).Object, StubPayments(), ProcessedPaymentsAcceptAll().Object, Uow());

        await sut.Invoking(s => s.EnsureReadingAllowedAsync(user.Id, SpreadType.ThreeCard))
            .Should().ThrowAsync<SubscriptionRequiredException>();
    }

    [Fact]
    public async Task EnsureReadingAllowed_treats_expired_subscription_as_inactive()
    {
        var user = NewUser(SubscriptionStatus.Active, DateTime.UtcNow.AddDays(-1));
        var sut = new SubscriptionService(UserRepoFor(user).Object, ReadingRepoWithCount(0).Object, StubPayments(), ProcessedPaymentsAcceptAll().Object, Uow());

        await sut.Invoking(s => s.EnsureReadingAllowedAsync(user.Id, SpreadType.ThreeCard))
            .Should().ThrowAsync<SubscriptionRequiredException>();
    }

    [Fact]
    public async Task EnsureReadingAllowed_throws_unauthorized_when_user_missing()
    {
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var sut = new SubscriptionService(users.Object, ReadingRepoWithCount(0).Object, StubPayments(), ProcessedPaymentsAcceptAll().Object, Uow());

        await sut.Invoking(s => s.EnsureReadingAllowedAsync(Guid.NewGuid(), SpreadType.SingleCard))
            .Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task GetStatus_reports_active_subscription()
    {
        var expires = DateTime.UtcNow.AddDays(10);
        var user = NewUser(SubscriptionStatus.Active, expires);
        var sut = new SubscriptionService(UserRepoFor(user).Object, ReadingRepoWithCount(5).Object, StubPayments(), ProcessedPaymentsAcceptAll().Object, Uow());

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
        var sut = new SubscriptionService(UserRepoFor(user).Object, ReadingRepoWithCount(1).Object, StubPayments(), ProcessedPaymentsAcceptAll().Object, Uow());

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

        var sut = new SubscriptionService(UserRepoFor(user).Object, ReadingRepoWithCount(0).Object, payments.Object, ProcessedPaymentsAcceptAll().Object, Uow());

        var result = await sut.CreatePaymentAsync(user.Id);

        result.PaymentId.Should().Be("pay-1");
        result.ConfirmationUrl.Should().Be("https://yk/confirm");
        result.Status.Should().Be("pending");
    }

    [Fact]
    public async Task CreatePayment_throws_when_subscription_already_active()
    {
        var user = NewUser(SubscriptionStatus.Active, DateTime.UtcNow.AddDays(5));
        var payments = new Mock<IPaymentProvider>();

        var sut = new SubscriptionService(UserRepoFor(user).Object, ReadingRepoWithCount(0).Object, payments.Object, ProcessedPaymentsAcceptAll().Object, Uow());

        await sut.Invoking(s => s.CreatePaymentAsync(user.Id))
            .Should().ThrowAsync<SubscriptionAlreadyActiveException>();
        payments.Verify(p => p.CreateSubscriptionPaymentAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<IPaymentProvider> PaymentsWithWebhook(
        PaymentWebhookEvent? evt,
        PaymentVerification? verification = null)
    {
        var payments = new Mock<IPaymentProvider>();
        payments.Setup(p => p.ParseWebhook(It.IsAny<string>())).Returns(evt);
        payments.Setup(p => p.VerifyPaymentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(verification);
        return payments;
    }

    private static PaymentVerification Verified(string paymentId, Guid userId, string status = "succeeded", bool paid = true) =>
        new()
        {
            PaymentId = paymentId,
            Status = status,
            Paid = paid,
            UserId = userId
        };

    [Fact]
    public async Task ProcessWebhook_ignores_replay_of_already_processed_payment()
    {
        var existingExpiry = DateTime.UtcNow.AddDays(10);
        var user = NewUser(SubscriptionStatus.Active, existingExpiry);
        user.YukassaSubscriptionId = "pay-latest";
        var users = UserRepoFor(user);
        var payments = PaymentsWithWebhook(
            new PaymentWebhookEvent { Type = PaymentWebhookEventType.PaymentSucceeded, PaymentId = "pay-replay", UserId = user.Id },
            Verified("pay-replay", user.Id));

        var processedRepo = new Mock<IProcessedPaymentRepository>();
        processedRepo.Setup(r => r.TryRecordAsync("pay-replay", user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var sut = new SubscriptionService(users.Object, ReadingRepoWithCount(0).Object, payments.Object, processedRepo.Object, Uow());

        var handled = await sut.ProcessWebhookAsync("{}");

        handled.Should().BeFalse();
        user.SubscriptionExpiresAt.Should().Be(existingExpiry);
        users.Verify(u => u.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessWebhook_blocks_replay_of_historical_payment_after_newer_payment_processed()
    {
        var existingExpiry = DateTime.UtcNow.AddDays(10);
        var user = NewUser(SubscriptionStatus.Active, existingExpiry);
        user.YukassaSubscriptionId = "pay-new";
        var users = UserRepoFor(user);
        var payments = PaymentsWithWebhook(
            new PaymentWebhookEvent { Type = PaymentWebhookEventType.PaymentSucceeded, PaymentId = "pay-old", UserId = user.Id },
            Verified("pay-old", user.Id));

        var processedRepo = new Mock<IProcessedPaymentRepository>();
        processedRepo.Setup(r => r.TryRecordAsync("pay-old", user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var sut = new SubscriptionService(users.Object, ReadingRepoWithCount(0).Object, payments.Object, processedRepo.Object, Uow());

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
        var payments = PaymentsWithWebhook(
            new PaymentWebhookEvent { Type = PaymentWebhookEventType.PaymentSucceeded, PaymentId = "pay-xyz", UserId = user.Id },
            Verified("pay-xyz", user.Id));

        var sut = new SubscriptionService(users.Object, ReadingRepoWithCount(0).Object, payments.Object, ProcessedPaymentsAcceptAll().Object, Uow());

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
        var payments = PaymentsWithWebhook(
            new PaymentWebhookEvent { Type = PaymentWebhookEventType.PaymentSucceeded, PaymentId = "pay-ext", UserId = user.Id },
            Verified("pay-ext", user.Id));

        var sut = new SubscriptionService(UserRepoFor(user).Object, ReadingRepoWithCount(0).Object, payments.Object, ProcessedPaymentsAcceptAll().Object, Uow());

        await sut.ProcessWebhookAsync("{}");

        user.SubscriptionExpiresAt.Should().BeAfter(existingExpiry.AddDays(SubscriptionService.SubscriptionDurationDays - 1));
    }

    [Fact]
    public async Task ProcessWebhook_ignores_cancelled_events()
    {
        var user = NewUser();
        var users = UserRepoFor(user);
        var payments = PaymentsWithWebhook(
            new PaymentWebhookEvent { Type = PaymentWebhookEventType.PaymentCanceled, PaymentId = "pay-cancel", UserId = user.Id });

        var sut = new SubscriptionService(users.Object, ReadingRepoWithCount(0).Object, payments.Object, ProcessedPaymentsAcceptAll().Object, Uow());

        var handled = await sut.ProcessWebhookAsync("{}");

        handled.Should().BeFalse();
        user.SubscriptionStatus.Should().Be(SubscriptionStatus.None);
        users.Verify(u => u.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessWebhook_ignores_unparseable_body()
    {
        var user = NewUser();
        var payments = PaymentsWithWebhook(null);

        var sut = new SubscriptionService(UserRepoFor(user).Object, ReadingRepoWithCount(0).Object, payments.Object, ProcessedPaymentsAcceptAll().Object, Uow());

        var handled = await sut.ProcessWebhookAsync("not-json");

        handled.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessWebhook_rejects_forged_body_when_yukassa_returns_no_payment()
    {
        var user = NewUser();
        var users = UserRepoFor(user);
        var payments = PaymentsWithWebhook(
            new PaymentWebhookEvent { Type = PaymentWebhookEventType.PaymentSucceeded, PaymentId = "fake-id", UserId = user.Id },
            verification: null);

        var sut = new SubscriptionService(users.Object, ReadingRepoWithCount(0).Object, payments.Object, ProcessedPaymentsAcceptAll().Object, Uow());

        var handled = await sut.ProcessWebhookAsync("{}");

        handled.Should().BeFalse();
        user.SubscriptionStatus.Should().Be(SubscriptionStatus.None);
        users.Verify(u => u.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessWebhook_rejects_payment_not_marked_paid_by_yukassa()
    {
        var user = NewUser();
        var users = UserRepoFor(user);
        var payments = PaymentsWithWebhook(
            new PaymentWebhookEvent { Type = PaymentWebhookEventType.PaymentSucceeded, PaymentId = "pay-pending", UserId = user.Id },
            Verified("pay-pending", user.Id, status: "pending", paid: false));

        var sut = new SubscriptionService(users.Object, ReadingRepoWithCount(0).Object, payments.Object, ProcessedPaymentsAcceptAll().Object, Uow());

        var handled = await sut.ProcessWebhookAsync("{}");

        handled.Should().BeFalse();
        users.Verify(u => u.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessWebhook_trusts_yukassa_user_id_over_webhook_body()
    {
        var realUser = NewUser();
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetByIdAsync(realUser.Id, It.IsAny<CancellationToken>())).ReturnsAsync(realUser);
        var victimId = Guid.NewGuid();
        var payments = PaymentsWithWebhook(
            new PaymentWebhookEvent { Type = PaymentWebhookEventType.PaymentSucceeded, PaymentId = "pay-real", UserId = victimId },
            Verified("pay-real", realUser.Id));

        var sut = new SubscriptionService(users.Object, ReadingRepoWithCount(0).Object, payments.Object, ProcessedPaymentsAcceptAll().Object, Uow());

        var handled = await sut.ProcessWebhookAsync("{}");

        handled.Should().BeTrue();
        users.Verify(u => u.GetByIdAsync(realUser.Id, It.IsAny<CancellationToken>()), Times.Once);
        users.Verify(u => u.GetByIdAsync(victimId, It.IsAny<CancellationToken>()), Times.Never);
    }
}
