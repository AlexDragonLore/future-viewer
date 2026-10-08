using FluentAssertions;
using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.DTOs;
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

    private static Mock<IReadingRepository> ReadingRepoWithCount(int count, int? total = null)
    {
        var readings = new Mock<IReadingRepository>();
        readings.Setup(r => r.CountTodayByUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(count);
        readings.Setup(r => r.CountByUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(total ?? count);
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
    public async Task EnsureReadingAllowed_allows_first_three_card_reading_for_free()
    {
        var user = NewUser();
        var sut = new SubscriptionService(UserRepoFor(user).Object, ReadingRepoWithCount(0).Object, StubPayments(), ProcessedPaymentsAcceptAll().Object, Uow());

        await sut.Invoking(s => s.EnsureReadingAllowedAsync(user.Id, SpreadType.ThreeCard))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureReadingAllowed_rejects_three_cards_after_any_previous_reading()
    {
        var user = NewUser();
        var sut = new SubscriptionService(UserRepoFor(user).Object, ReadingRepoWithCount(0, total: 1).Object,
            StubPayments(), ProcessedPaymentsAcceptAll().Object, Uow());

        await sut.Invoking(s => s.EnsureReadingAllowedAsync(user.Id, SpreadType.ThreeCard))
            .Should().ThrowAsync<SubscriptionRequiredException>();
        await sut.Invoking(s => s.EnsureReadingAllowedAsync(user.Id, SpreadType.SingleCard))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureReadingAllowed_does_not_restore_intro_after_all_readings_are_erased()
    {
        var user = NewUser();
        user.HasUsedIntroReading = true;
        var sut = new SubscriptionService(UserRepoFor(user).Object, ReadingRepoWithCount(0).Object,
            StubPayments(), ProcessedPaymentsAcceptAll().Object, Uow());

        await sut.Invoking(s => s.EnsureReadingAllowedAsync(user.Id, SpreadType.ThreeCard))
            .Should().ThrowAsync<SubscriptionRequiredException>();
        (await sut.GetStatusAsync(user.Id)).CanCreateIntroReading.Should().BeFalse();
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, true)]
    public async Task Erased_reading_usage_preserves_quota_until_the_next_utc_day(int dayOffset, bool allowed)
    {
        var user = NewUser();
        user.HasUsedIntroReading = true;
        user.LastReadingAt = DateTime.UtcNow.Date.AddDays(dayOffset);
        var sut = new SubscriptionService(UserRepoFor(user).Object, ReadingRepoWithCount(0).Object,
            StubPayments(), ProcessedPaymentsAcceptAll().Object, Uow());

        (await sut.GetStatusAsync(user.Id)).CanCreateFreeReading.Should().Be(allowed);
        var act = () => sut.EnsureReadingAllowedAsync(user.Id, SpreadType.SingleCard);
        if (allowed) await act.Should().NotThrowAsync();
        else await act.Should().ThrowAsync<QuotaExceededException>();
    }

    [Fact]
    public async Task EnsureReadingAllowed_rejects_celtic_cross_even_for_a_new_free_user()
    {
        var user = NewUser();
        var sut = new SubscriptionService(UserRepoFor(user).Object, ReadingRepoWithCount(0).Object,
            StubPayments(), ProcessedPaymentsAcceptAll().Object, Uow());

        await sut.Invoking(s => s.EnsureReadingAllowedAsync(user.Id, SpreadType.CelticCross))
            .Should().ThrowAsync<SubscriptionRequiredException>();
    }

    [Fact]
    public async Task EnsureReadingAllowed_treats_expired_subscription_as_inactive()
    {
        var user = NewUser(SubscriptionStatus.Active, DateTime.UtcNow.AddDays(-1));
        var sut = new SubscriptionService(UserRepoFor(user).Object, ReadingRepoWithCount(0, total: 1).Object, StubPayments(), ProcessedPaymentsAcceptAll().Object, Uow());

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
        status.CanCreateIntroReading.Should().BeFalse();
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
        status.CanCreateIntroReading.Should().BeFalse();
    }

    [Fact]
    public async Task GetStatus_reports_intro_reading_for_a_new_free_user()
    {
        var user = NewUser();
        var sut = new SubscriptionService(UserRepoFor(user).Object, ReadingRepoWithCount(0).Object,
            StubPayments(), ProcessedPaymentsAcceptAll().Object, Uow());

        var status = await sut.GetStatusAsync(user.Id);

        status.CanCreateIntroReading.Should().BeTrue();
        status.CanCreateFreeReading.Should().BeTrue();
    }

    [Fact]
    public async Task AddReading_locks_the_user_and_persists_consumed_intro_entitlement()
    {
        var user = NewUser();
        var users = UserRepoFor(user);
        var readings = ReadingRepoWithCount(0);
        var reading = new Reading { UserId = user.Id, SpreadType = SpreadType.ThreeCard, Question = "q" };
        readings.Setup(r => r.AddAsync(reading, It.IsAny<CancellationToken>())).ReturnsAsync(reading);
        var sut = new SubscriptionService(users.Object, readings.Object,
            StubPayments(), ProcessedPaymentsAcceptAll().Object, Uow());

        (await sut.AddReadingAsync(reading)).Should().BeSameAs(reading);

        users.Verify(r => r.LockAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
        users.Verify(r => r.UpdateAsync(It.Is<User>(u => u.HasUsedIntroReading), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static CreatePaymentRequest AcceptedOffer(string tariffCode = "pro-30d") => new()
    {
        TariffCode = tariffCode, OfferAccepted = true, OfferVersion = "test-offer-v1"
    };

    private static Mock<IPrivacyRepository> PrivacyRepoForOffer()
    {
        var privacy = new Mock<IPrivacyRepository>();
        privacy.Setup(x => x.GetActiveLegalDocumentAsync(
            LegalDocumentType.PublicOffer, "test-offer-v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LegalDocument
            {
                DocumentType = LegalDocumentType.PublicOffer,
                Version = "test-offer-v1",
                ContentHash = new string('a', 64),
                PublishedAt = DateTime.UtcNow.AddDays(-1),
                EffectiveAt = DateTime.UtcNow.AddDays(-1),
                IsActive = true
            });
        return privacy;
    }

    private static Mock<IPaymentProvider> PaymentProvider(decimal price = 299m)
    {
        var payments = new Mock<IPaymentProvider>();
        payments.SetupGet(x => x.ProviderName).Returns("YooKassa");
        var monthly = new PaymentProductDescriptor
        {
            TariffCode = "pro-30d",
            Amount = price,
            Currency = "RUB",
            AccessDays = 30
        };
        payments.SetupGet(x => x.Product).Returns(monthly);
        payments.SetupGet(x => x.Products).Returns([
            new PaymentProductDescriptor
            {
                TariffCode = "pro-7d", Amount = 99m, Currency = "RUB", AccessDays = 7
            },
            monthly
        ]);
        return payments;
    }

    private static PaymentOrder NewOrder(User user, string? paymentId = "pay-1", decimal amount = 300m, int accessDays = 30) => new()
    {
        UserId = user.Id,
        SubjectReference = user.PrivacySubjectId,
        TariffCode = "original-30d",
        Amount = amount,
        Currency = "RUB",
        AccessDays = accessDays,
        Provider = "YooKassa",
        IdempotencyKey = Guid.NewGuid().ToString("N"),
        ProviderPaymentId = paymentId,
        Status = PaymentOrderStatus.ProviderCreated
    };

    private static Mock<IPaymentOrderRepository> OrderRepoFor(PaymentOrder order)
    {
        var orders = new Mock<IPaymentOrderRepository>();
        orders.Setup(x => x.GetByPublicIdForUpdateAsync(order.PublicId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        return orders;
    }

    private static void SetupWebhook(
        Mock<IPaymentProvider> payments,
        PaymentOrder order,
        string paymentId = "pay-1",
        decimal amount = 300m,
        string currency = "RUB",
        bool paid = true,
        string status = "succeeded",
        Guid? untrustedUserId = null,
        string? verifiedPaymentId = null)
    {
        payments.Setup(x => x.ParseWebhook(It.IsAny<string>())).Returns(new PaymentWebhookEvent
        {
            Type = PaymentWebhookEventType.PaymentSucceeded,
            PaymentId = paymentId,
            UserId = untrustedUserId
        });
        payments.Setup(x => x.VerifyPaymentAsync(paymentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentVerification
            {
                PaymentId = verifiedPaymentId ?? paymentId,
                OrderId = order.PublicId,
                UserId = untrustedUserId,
                Amount = amount,
                Currency = currency,
                Paid = paid,
                Status = status
            });
    }

    [Theory]
    [InlineData(false, "pro-7d", 99, 7)]
    [InlineData(true, "pro-7d", 99, 7)]
    [InlineData(false, "pro-30d", 299, 30)]
    [InlineData(true, "pro-30d", 299, 30)]
    public async Task CreatePayment_persists_order_and_sends_no_user_identifiers(
        bool activeSubscriber, string tariffCode, decimal price, int accessDays)
    {
        var user = NewUser(activeSubscriber ? SubscriptionStatus.Active : SubscriptionStatus.None,
            activeSubscriber ? DateTime.UtcNow.AddDays(5) : null);
        var payments = PaymentProvider();
        PaymentOrder? persisted = null;
        var orders = new Mock<IPaymentOrderRepository>();
        orders.Setup(x => x.AddAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()))
            .Callback<PaymentOrder, CancellationToken>((order, _) => persisted = order)
            .ReturnsAsync((PaymentOrder order, CancellationToken _) => order);
        payments.Setup(x => x.CreateSubscriptionPaymentAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<PaymentProductDescriptor>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentCreationResult
            {
                PaymentId = "provider-payment-1",
                ConfirmationUrl = "https://provider.example/hosted",
                Status = "pending"
            });
        var privacy = PrivacyRepoForOffer();
        var sut = new SubscriptionService(UserRepoFor(user).Object, ReadingRepoWithCount(0).Object,
            payments.Object, ProcessedPaymentsAcceptAll().Object, Uow(), orders.Object, privacy: privacy.Object);

        var result = await sut.CreatePaymentAsync(user.Id, AcceptedOffer(tariffCode));

        persisted.Should().NotBeNull();
        persisted!.TariffCode.Should().Be(tariffCode);
        persisted.Amount.Should().Be(price);
        persisted.Currency.Should().Be("RUB");
        persisted.AccessDays.Should().Be(accessDays);
        persisted.UserId.Should().Be(user.Id);
        persisted.SubjectReference.Should().Be(user.PrivacySubjectId);
        persisted.ProviderPaymentId.Should().Be("provider-payment-1");
        result.PaymentId.Should().Be(persisted.PublicId.ToString("N"));
        result.ConfirmationUrl.Should().Be("https://provider.example/hosted");
        privacy.Verify(x => x.AddConsentAsync(It.Is<UserConsent>(consent =>
            consent.UserId == user.Id
            && consent.ConsentType == ConsentType.OfferAcceptance
            && consent.DocumentVersion == "test-offer-v1"
            && consent.ContentHash == new string('a', 64)
            && consent.CollectionSource == $"checkout:{persisted.PublicId:N}"),
            It.IsAny<CancellationToken>()), Times.Once);
        payments.Verify(x => x.CreateSubscriptionPaymentAsync(
            persisted.PublicId, persisted.IdempotencyKey, It.Is<PaymentProductDescriptor>(product =>
                product.TariffCode == tariffCode && product.Amount == price && product.AccessDays == accessDays),
            It.IsAny<CancellationToken>()), Times.Once);
        payments.Verify(x => x.CreateSubscriptionPaymentAsync(
            user.Id, user.Email, It.IsAny<PaymentProductDescriptor>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreatePayment_without_local_order_storage_fails_before_contacting_provider()
    {
        var user = NewUser();
        var payments = PaymentProvider();
        var sut = new SubscriptionService(UserRepoFor(user).Object, ReadingRepoWithCount(0).Object,
            payments.Object, ProcessedPaymentsAcceptAll().Object, Uow());

        await sut.Invoking(x => x.CreatePaymentAsync(user.Id, AcceptedOffer())).Should().ThrowAsync<InvalidOperationException>();

        payments.Verify(x => x.CreateSubscriptionPaymentAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<PaymentProductDescriptor>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(false, "test-offer-v1")]
    [InlineData(true, "")]
    [InlineData(true, "outdated-offer")]
    public async Task CreatePayment_rejects_missing_acceptance_or_inactive_offer_before_contacting_provider(
        bool accepted, string version)
    {
        var user = NewUser();
        var payments = PaymentProvider();
        var orders = new Mock<IPaymentOrderRepository>();
        var privacy = PrivacyRepoForOffer();
        var sut = new SubscriptionService(UserRepoFor(user).Object, ReadingRepoWithCount(0).Object,
            payments.Object, ProcessedPaymentsAcceptAll().Object, Uow(), orders.Object, privacy: privacy.Object);

        await sut.Invoking(x => x.CreatePaymentAsync(user.Id,
            new CreatePaymentRequest { OfferAccepted = accepted, OfferVersion = version }))
            .Should().ThrowAsync<DomainException>();

        orders.Verify(x => x.AddAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()), Times.Never);
        privacy.Verify(x => x.AddConsentAsync(It.IsAny<UserConsent>(), It.IsAny<CancellationToken>()), Times.Never);
        payments.Verify(x => x.CreateSubscriptionPaymentAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<PaymentProductDescriptor>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("pro-1d")]
    [InlineData("PRO-7D")]
    public async Task CreatePayment_rejects_unknown_tariffs_without_creating_orders_or_charging(string tariffCode)
    {
        var user = NewUser();
        var payments = PaymentProvider();
        var orders = new Mock<IPaymentOrderRepository>();
        var privacy = PrivacyRepoForOffer();
        var sut = new SubscriptionService(UserRepoFor(user).Object, ReadingRepoWithCount(0).Object,
            payments.Object, ProcessedPaymentsAcceptAll().Object, Uow(), orders.Object, privacy: privacy.Object);

        await sut.Invoking(x => x.CreatePaymentAsync(user.Id, AcceptedOffer(tariffCode)))
            .Should().ThrowAsync<DomainException>();

        orders.Verify(x => x.AddAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()), Times.Never);
        privacy.Verify(x => x.AddConsentAsync(It.IsAny<UserConsent>(), It.IsAny<CancellationToken>()), Times.Never);
        payments.Verify(x => x.CreateSubscriptionPaymentAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<PaymentProductDescriptor>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessWebhook_without_local_order_storage_never_activates_access()
    {
        var user = NewUser();
        var users = UserRepoFor(user);
        var payments = PaymentProvider();
        SetupWebhook(payments, NewOrder(user), untrustedUserId: user.Id);
        var sut = new SubscriptionService(users.Object, ReadingRepoWithCount(0).Object,
            payments.Object, ProcessedPaymentsAcceptAll().Object, Uow());

        (await sut.ProcessWebhookWithOutcomeAsync("{}")).Should().Be(PaymentWebhookHandling.Rejected);

        users.Verify(x => x.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(false, 30, 300)]
    [InlineData(true, 30, 300)]
    [InlineData(false, 7, 99)]
    [InlineData(true, 7, 99)]
    public async Task ProcessWebhook_activates_or_extends_access_using_original_order_price_and_term(
        bool alreadyActive, int accessDays, decimal originalAmount)
    {
        var existingExpiry = DateTime.UtcNow.AddDays(5);
        var user = NewUser(alreadyActive ? SubscriptionStatus.Active : SubscriptionStatus.None,
            alreadyActive ? existingExpiry : null);
        var order = NewOrder(user, amount: originalAmount, accessDays: accessDays);
        var payments = PaymentProvider();
        SetupWebhook(payments, order, amount: originalAmount);
        var orders = OrderRepoFor(order);
        var sut = new SubscriptionService(UserRepoFor(user).Object, ReadingRepoWithCount(0).Object,
            payments.Object, ProcessedPaymentsAcceptAll().Object, Uow(), orders.Object, privacy: PrivacyRepoForOffer().Object);

        (await sut.ProcessWebhookWithOutcomeAsync("{}")).Should().Be(PaymentWebhookHandling.Processed);

        user.SubscriptionStatus.Should().Be(SubscriptionStatus.Active);
        user.SubscriptionExpiresAt.Should().BeCloseTo(
            (alreadyActive ? existingExpiry : DateTime.UtcNow).AddDays(order.AccessDays), TimeSpan.FromSeconds(2));
        user.YukassaSubscriptionId.Should().Be("pay-1");
        order.Status.Should().Be(PaymentOrderStatus.Paid);
        order.ReceiptStatus.Should().Be(NpdReceiptStatus.PendingManualIssue);
        order.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Redirect_payment_receives_operation_id_only_after_authenticated_notification()
    {
        var user = NewUser();
        var payments = PaymentProvider();
        PaymentOrder? persisted = null;
        var orders = new Mock<IPaymentOrderRepository>();
        orders.Setup(x => x.AddAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()))
            .Callback<PaymentOrder, CancellationToken>((order, _) => persisted = order)
            .ReturnsAsync((PaymentOrder order, CancellationToken _) => order);
        payments.Setup(x => x.CreateSubscriptionPaymentAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<PaymentProductDescriptor>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentCreationResult
            {
                PaymentId = null,
                ConfirmationUrl = "https://provider.example/redirect",
                Status = "pending"
            });
        var sut = new SubscriptionService(UserRepoFor(user).Object, ReadingRepoWithCount(0).Object,
            payments.Object, ProcessedPaymentsAcceptAll().Object, Uow(), orders.Object, privacy: PrivacyRepoForOffer().Object);

        await sut.CreatePaymentAsync(user.Id, AcceptedOffer());
        persisted!.ProviderPaymentId.Should().BeNull();
        persisted.TariffCode.Should().Be("pro-30d", "legacy clients omit the tariff code");
        persisted.Amount.Should().Be(299m);
        persisted.AccessDays.Should().Be(30);
        orders.Setup(x => x.GetByPublicIdForUpdateAsync(persisted.PublicId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => persisted);
        SetupWebhook(payments, persisted, paymentId: "real-transfer-operation-id", amount: persisted.Amount);

        (await sut.ProcessWebhookWithOutcomeAsync("{}")).Should().Be(PaymentWebhookHandling.Processed);
        persisted.ProviderPaymentId.Should().Be("real-transfer-operation-id");
    }

    [Fact]
    public async Task ProcessWebhook_ignores_replay_without_resetting_receipt_or_extending_access()
    {
        var expiry = DateTime.UtcNow.AddDays(10);
        var user = NewUser(SubscriptionStatus.Active, expiry);
        var order = NewOrder(user);
        order.Status = PaymentOrderStatus.Paid;
        order.ReceiptStatus = NpdReceiptStatus.Issued;
        var payments = PaymentProvider();
        SetupWebhook(payments, order);
        var processed = new Mock<IProcessedPaymentRepository>();
        processed.Setup(x => x.TryRecordAsync("pay-1", user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var users = UserRepoFor(user);
        var sut = new SubscriptionService(users.Object, ReadingRepoWithCount(0).Object,
            payments.Object, processed.Object, Uow(), OrderRepoFor(order).Object);

        (await sut.ProcessWebhookWithOutcomeAsync("{}")).Should().Be(PaymentWebhookHandling.Acknowledged);

        user.SubscriptionExpiresAt.Should().Be(expiry);
        order.ReceiptStatus.Should().Be(NpdReceiptStatus.Issued);
        users.Verify(x => x.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(true, true, "succeeded", "legacy-paid", PaymentWebhookHandling.Acknowledged)]
    [InlineData(false, true, "succeeded", "legacy-paid", PaymentWebhookHandling.Rejected)]
    [InlineData(true, false, "succeeded", "legacy-paid", PaymentWebhookHandling.Rejected)]
    [InlineData(true, true, "pending", "legacy-paid", PaymentWebhookHandling.Rejected)]
    [InlineData(true, true, "succeeded", "different-payment", PaymentWebhookHandling.Rejected)]
    public async Task Legacy_payment_replay_requires_provider_verification_and_existing_receipt_without_mutations(
        bool recorded, bool paid, string status, string verifiedId, PaymentWebhookHandling expected)
    {
        var expiry = DateTime.UtcNow.AddDays(10);
        var user = NewUser(SubscriptionStatus.Active, expiry);
        var users = UserRepoFor(user);
        var orders = new Mock<IPaymentOrderRepository>(MockBehavior.Strict);
        var processed = new Mock<IProcessedPaymentRepository>();
        processed.Setup(x => x.ExistsAsync("legacy-paid", It.IsAny<CancellationToken>())).ReturnsAsync(recorded);
        var payments = PaymentProvider();
        payments.Setup(x => x.ParseWebhook(It.IsAny<string>())).Returns(new PaymentWebhookEvent
        {
            Type = PaymentWebhookEventType.PaymentSucceeded,
            PaymentId = "legacy-paid",
            UserId = Guid.NewGuid()
        });
        payments.Setup(x => x.VerifyPaymentAsync("legacy-paid", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentVerification
            {
                PaymentId = verifiedId,
                Paid = paid,
                Status = status,
                Amount = 300m,
                Currency = "RUB",
                OrderId = null
            });
        var sut = new SubscriptionService(users.Object, ReadingRepoWithCount(0).Object,
            payments.Object, processed.Object, Uow(), orders.Object);

        (await sut.ProcessWebhookWithOutcomeAsync("{}")).Should().Be(expected);

        user.SubscriptionExpiresAt.Should().Be(expiry);
        users.Verify(x => x.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        processed.Verify(x => x.TryRecordAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        payments.Verify(x => x.VerifyPaymentAsync("legacy-paid", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(299, "RUB", true, "succeeded")]
    [InlineData(301, "RUB", true, "succeeded")]
    [InlineData(300, "USD", true, "succeeded")]
    [InlineData(300, "RUB", false, "succeeded")]
    [InlineData(300, "RUB", true, "pending")]
    public async Task ProcessWebhook_rejects_invalid_payment_without_mutations(
        decimal amount, string currency, bool paid, string status)
    {
        var user = NewUser();
        var order = NewOrder(user);
        var payments = PaymentProvider();
        SetupWebhook(payments, order, amount: amount, currency: currency, paid: paid, status: status);
        var users = UserRepoFor(user);
        var orders = OrderRepoFor(order);
        var processed = ProcessedPaymentsAcceptAll();
        var sut = new SubscriptionService(users.Object, ReadingRepoWithCount(0).Object,
            payments.Object, processed.Object, Uow(), orders.Object, privacy: PrivacyRepoForOffer().Object);

        (await sut.ProcessWebhookWithOutcomeAsync("{}")).Should().Be(PaymentWebhookHandling.Rejected);

        users.Verify(x => x.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        orders.Verify(x => x.UpdateAsync(It.IsAny<PaymentOrder>(), It.IsAny<CancellationToken>()), Times.Never);
        processed.Verify(x => x.TryRecordAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(PaymentOrderStatus.Canceled)]
    [InlineData(PaymentOrderStatus.Failed)]
    public async Task ProcessWebhook_rejects_closed_orders(PaymentOrderStatus status)
    {
        var user = NewUser();
        var order = NewOrder(user);
        order.Status = status;
        var payments = PaymentProvider();
        SetupWebhook(payments, order);
        var sut = new SubscriptionService(UserRepoFor(user).Object, ReadingRepoWithCount(0).Object,
            payments.Object, ProcessedPaymentsAcceptAll().Object, Uow(), OrderRepoFor(order).Object);

        (await sut.ProcessWebhookWithOutcomeAsync("{}")).Should().Be(PaymentWebhookHandling.Rejected);
        user.SubscriptionStatus.Should().Be(SubscriptionStatus.None);
    }

    [Theory]
    [InlineData("pay-other", null)]
    [InlineData("pay-1", "pay-other")]
    public async Task ProcessWebhook_rejects_mismatched_provider_payment_ids(string incomingId, string? verifiedId)
    {
        var user = NewUser();
        var order = NewOrder(user);
        var payments = PaymentProvider();
        SetupWebhook(payments, order, paymentId: incomingId, verifiedPaymentId: verifiedId);
        var sut = new SubscriptionService(UserRepoFor(user).Object, ReadingRepoWithCount(0).Object,
            payments.Object, ProcessedPaymentsAcceptAll().Object, Uow(), OrderRepoFor(order).Object);

        (await sut.ProcessWebhookWithOutcomeAsync("{}")).Should().Be(PaymentWebhookHandling.Rejected);
        user.SubscriptionStatus.Should().Be(SubscriptionStatus.None);
    }

    [Fact]
    public async Task ProcessWebhook_uses_only_local_order_owner_and_ignores_provider_user_metadata()
    {
        var user = NewUser();
        var users = UserRepoFor(user);
        var order = NewOrder(user);
        var victimId = Guid.NewGuid();
        var payments = PaymentProvider();
        SetupWebhook(payments, order, untrustedUserId: victimId);
        var sut = new SubscriptionService(users.Object, ReadingRepoWithCount(0).Object,
            payments.Object, ProcessedPaymentsAcceptAll().Object, Uow(), OrderRepoFor(order).Object);

        (await sut.ProcessWebhookWithOutcomeAsync("{}")).Should().Be(PaymentWebhookHandling.Processed);

        users.Verify(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
        users.Verify(x => x.GetByIdAsync(victimId, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessWebhook_rejects_unknown_order_without_falling_back_to_user_metadata()
    {
        var user = NewUser();
        var payments = PaymentProvider();
        SetupWebhook(payments, NewOrder(user), untrustedUserId: user.Id);
        var users = UserRepoFor(user);
        var sut = new SubscriptionService(users.Object, ReadingRepoWithCount(0).Object,
            payments.Object, ProcessedPaymentsAcceptAll().Object, Uow(), new Mock<IPaymentOrderRepository>().Object);

        (await sut.ProcessWebhookWithOutcomeAsync("{}")).Should().Be(PaymentWebhookHandling.Rejected);
        users.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessWebhook_rejects_missing_provider_verification()
    {
        var user = NewUser();
        var order = NewOrder(user);
        var payments = PaymentProvider();
        SetupWebhook(payments, order);
        payments.Setup(x => x.VerifyPaymentAsync("pay-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentVerification?)null);
        var sut = new SubscriptionService(UserRepoFor(user).Object, ReadingRepoWithCount(0).Object,
            payments.Object, ProcessedPaymentsAcceptAll().Object, Uow(), OrderRepoFor(order).Object);

        (await sut.ProcessWebhookWithOutcomeAsync("{}")).Should().Be(PaymentWebhookHandling.Rejected);
    }

    [Theory]
    [InlineData(PaymentWebhookEventType.PaymentCanceled, PaymentWebhookHandling.Acknowledged)]
    [InlineData(PaymentWebhookEventType.Unknown, PaymentWebhookHandling.Rejected)]
    public async Task ProcessWebhook_does_not_activate_for_non_success_events(
        PaymentWebhookEventType type, PaymentWebhookHandling expected)
    {
        var user = NewUser();
        var payments = PaymentProvider();
        payments.Setup(x => x.ParseWebhook(It.IsAny<string>())).Returns(new PaymentWebhookEvent
        {
            Type = type,
            PaymentId = "pay-1"
        });
        var sut = new SubscriptionService(UserRepoFor(user).Object, ReadingRepoWithCount(0).Object,
            payments.Object, ProcessedPaymentsAcceptAll().Object, Uow(), new Mock<IPaymentOrderRepository>().Object);

        (await sut.ProcessWebhookWithOutcomeAsync("{}")).Should().Be(expected);
        user.SubscriptionStatus.Should().Be(SubscriptionStatus.None);
    }
}
