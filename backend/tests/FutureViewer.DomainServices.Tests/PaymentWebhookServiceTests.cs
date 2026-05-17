using FluentAssertions;
using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Interfaces;
using FutureViewer.DomainServices.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FutureViewer.DomainServices.Tests;

public sealed class PaymentWebhookServiceTests
{
    [Fact]
    public async Task ProcessWebhook_routes_subscription_payment_to_subscription_service()
    {
        var userId = Guid.NewGuid();
        var users = UserRepo(userId);
        var processed = ProcessedPayments(true);
        var payments = Payments(
            PaymentProductType.Subscription,
            userId,
            tarotPlusSessionId: null);
        var subscription = new SubscriptionService(users.Object, Mock.Of<IReadingRepository>(), payments.Object, processed.Object, Uow());
        var sut = new PaymentWebhookService(payments.Object, processed.Object, Uow(), subscription, CreateTarotPlusService(userId, out _));

        var handled = await sut.ProcessWebhookAsync("{}");

        handled.Should().BeTrue();
        users.Verify(x => x.UpdateAsync(
            It.Is<User>(u => u.YukassaSubscriptionId == "pay-1" && u.TarotPlusCredits == 1),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessWebhook_routes_tarot_plus_payment_to_tarot_plus_service()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var payments = Payments(PaymentProductType.TarotPlusSession, userId, sessionId);
        var tarotPlus = CreateTarotPlusService(userId, out var tarotPlusRepo, sessionId);
        var sut = new PaymentWebhookService(
            payments.Object,
            ProcessedPayments(true).Object,
            Uow(),
            new SubscriptionService(UserRepo(userId).Object, Mock.Of<IReadingRepository>(), payments.Object, ProcessedPayments(true).Object, Uow()),
            tarotPlus);

        var handled = await sut.ProcessWebhookAsync("{}");

        handled.Should().BeTrue();
        tarotPlusRepo.Items.Single().Status.Should().Be(TarotPlusSessionStatus.Paid);
        tarotPlusRepo.Items.Single().PaymentId.Should().Be("pay-1");
    }

    [Fact]
    public async Task ProcessWebhook_uses_verified_tarot_plus_session_over_raw_webhook_metadata()
    {
        var userId = Guid.NewGuid();
        var rawSessionId = Guid.NewGuid();
        var verifiedSessionId = Guid.NewGuid();
        var payments = Payments(
            PaymentProductType.TarotPlusSession,
            userId,
            rawTarotPlusSessionId: rawSessionId,
            verifiedTarotPlusSessionId: verifiedSessionId);
        var tarotPlus = CreateTarotPlusService(userId, out var tarotPlusRepo, verifiedSessionId);
        tarotPlusRepo.Items.Add(new TarotPlusSession
        {
            Id = rawSessionId,
            UserId = userId,
            Status = TarotPlusSessionStatus.PaymentPending,
            Route = TarotPlusRoute.GeneralLife,
            CoreRequest = "Raw",
            PreviewText = "Raw preview"
        });
        var sut = new PaymentWebhookService(
            payments.Object,
            ProcessedPayments(true).Object,
            Uow(),
            new SubscriptionService(UserRepo(userId).Object, Mock.Of<IReadingRepository>(), payments.Object, ProcessedPayments(true).Object, Uow()),
            tarotPlus);

        var handled = await sut.ProcessWebhookAsync("{}");

        handled.Should().BeTrue();
        tarotPlusRepo.Items.Single(x => x.Id == verifiedSessionId).Status.Should().Be(TarotPlusSessionStatus.Paid);
        tarotPlusRepo.Items.Single(x => x.Id == rawSessionId).Status.Should().Be(TarotPlusSessionStatus.PaymentPending);
    }

    [Fact]
    public async Task ProcessWebhook_rejects_tarot_plus_payment_without_verified_session_metadata()
    {
        var userId = Guid.NewGuid();
        var rawSessionId = Guid.NewGuid();
        var processed = ProcessedPayments(true);
        var payments = Payments(
            PaymentProductType.TarotPlusSession,
            userId,
            rawTarotPlusSessionId: rawSessionId,
            verifiedTarotPlusSessionId: null);
        var tarotPlus = CreateTarotPlusService(userId, out var tarotPlusRepo, rawSessionId);
        var sut = new PaymentWebhookService(
            payments.Object,
            processed.Object,
            Uow(),
            new SubscriptionService(UserRepo(userId).Object, Mock.Of<IReadingRepository>(), payments.Object, processed.Object, Uow()),
            tarotPlus);

        var handled = await sut.ProcessWebhookAsync("{}");

        handled.Should().BeFalse();
        tarotPlusRepo.Items.Single().Status.Should().Be(TarotPlusSessionStatus.PaymentPending);
        processed.Verify(
            x => x.TryRecordAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessWebhook_ignores_unpaid_payment()
    {
        var userId = Guid.NewGuid();
        var payments = Payments(PaymentProductType.Subscription, userId, null, paid: false);
        var sut = new PaymentWebhookService(
            payments.Object,
            ProcessedPayments(true).Object,
            Uow(),
            new SubscriptionService(UserRepo(userId).Object, Mock.Of<IReadingRepository>(), payments.Object, ProcessedPayments(true).Object, Uow()),
            CreateTarotPlusService(userId, out _));

        var handled = await sut.ProcessWebhookAsync("{}");

        handled.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessWebhook_ignores_duplicate_processed_payment()
    {
        var userId = Guid.NewGuid();
        var payments = Payments(PaymentProductType.Subscription, userId, null);
        var users = UserRepo(userId);
        var sut = new PaymentWebhookService(
            payments.Object,
            ProcessedPayments(false).Object,
            Uow(),
            new SubscriptionService(users.Object, Mock.Of<IReadingRepository>(), payments.Object, ProcessedPayments(false).Object, Uow()),
            CreateTarotPlusService(userId, out _));

        var handled = await sut.ProcessWebhookAsync("{}");

        handled.Should().BeFalse();
        users.Verify(x => x.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<IPaymentProvider> Payments(
        PaymentProductType productType,
        Guid userId,
        Guid? tarotPlusSessionId = null,
        Guid? rawTarotPlusSessionId = null,
        Guid? verifiedTarotPlusSessionId = null,
        bool paid = true)
    {
        var payments = new Mock<IPaymentProvider>();
        payments.Setup(x => x.ParseWebhook(It.IsAny<string>())).Returns(new PaymentWebhookEvent
        {
            Type = PaymentWebhookEventType.PaymentSucceeded,
            PaymentId = "pay-1",
            UserId = userId,
            ProductType = productType,
            TarotPlusSessionId = rawTarotPlusSessionId ?? tarotPlusSessionId
        });
        payments.Setup(x => x.VerifyPaymentAsync("pay-1", It.IsAny<CancellationToken>())).ReturnsAsync(new PaymentVerification
        {
            PaymentId = "pay-1",
            Status = paid ? "succeeded" : "pending",
            Paid = paid,
            UserId = userId,
            ProductType = productType,
            TarotPlusSessionId = verifiedTarotPlusSessionId ?? tarotPlusSessionId
        });
        return payments;
    }

    private static Mock<IUserRepository> UserRepo(Guid userId)
    {
        var user = new User { Id = userId, Email = "pay@example.com", PasswordHash = "hash" };
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        return users;
    }

    private static Mock<IProcessedPaymentRepository> ProcessedPayments(bool accepted)
    {
        var processed = new Mock<IProcessedPaymentRepository>();
        processed.Setup(x => x.TryRecordAsync("pay-1", It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(accepted);
        return processed;
    }

    private static IUnitOfWork Uow() => new PassThroughUnitOfWork();

    private static TarotPlusService CreateTarotPlusService(
        Guid userId,
        out InMemoryTarotPlusSessionRepository repo,
        Guid? sessionId = null)
    {
        repo = new InMemoryTarotPlusSessionRepository();
        if (sessionId is { } id)
        {
            repo.Items.Add(new TarotPlusSession
            {
                Id = id,
                UserId = userId,
                Status = TarotPlusSessionStatus.PaymentPending,
                Route = TarotPlusRoute.GeneralLife,
                CoreRequest = "Core",
                PreviewText = "Preview"
            });
        }

        return new TarotPlusService(
            repo,
            UserRepo(userId).Object,
            Mock.Of<IPaymentProvider>(),
            Uow(),
            new CardDeckService(new TestDeck()),
            new FakeTarotPlusAI(),
            NullLogger<TarotPlusService>.Instance);
    }

    private sealed class PassThroughUnitOfWork : IUnitOfWork
    {
        public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default) =>
            work(ct);
    }

    private sealed class FakeTarotPlusAI : ITarotPlusAI
    {
        public string Model => "fake";

        public Task<TarotPlusRouteResult> RouteAsync(TarotPlusInterviewContext context, CancellationToken ct = default) =>
            Task.FromResult(new TarotPlusRouteResult { Route = TarotPlusRoute.GeneralLife, PreviewText = "Preview", SafetyFlags = Array.Empty<string>() });

        public Task<TarotPlusQuestionResult> NextQuestionAsync(TarotPlusInterviewContext context, CancellationToken ct = default) =>
            Task.FromResult(new TarotPlusQuestionResult { Question = "Q", ReadyForReport = true, SafetyFlags = Array.Empty<string>() });

        public Task<TarotPlusReportResult> GenerateReportAsync(TarotPlusReportContext context, CancellationToken ct = default) =>
            Task.FromResult(new TarotPlusReportResult { ReportMarkdown = "# Report" });

        public Task<TarotPlusFollowUpResult> AskFollowUpAsync(TarotPlusFollowUpContext context, CancellationToken ct = default) =>
            Task.FromResult(new TarotPlusFollowUpResult { AnswerMarkdown = "A" });
    }

    private sealed class InMemoryTarotPlusSessionRepository : ITarotPlusSessionRepository
    {
        public List<TarotPlusSession> Items { get; } = new();

        public Task<TarotPlusSession> AddAsync(TarotPlusSession session, CancellationToken ct = default)
        {
            Items.Add(session);
            return Task.FromResult(session);
        }

        public Task<TarotPlusSession?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.Id == id));

        public Task<TarotPlusSession?> GetByPaymentIdAsync(string paymentId, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.PaymentId == paymentId));

        public Task<IReadOnlyList<TarotPlusSession>> GetHistoryAsync(Guid userId, int take = 20, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TarotPlusSession>>(Items.Where(x => x.UserId == userId).Take(take).ToList());

        public Task<IReadOnlyList<TarotPlusSession>> SearchAsync(
            Guid? userId,
            TarotPlusSessionStatus? status,
            int skip,
            int take,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TarotPlusSession>>(Items
                .Where(x => !userId.HasValue || x.UserId == userId.Value)
                .Where(x => !status.HasValue || x.Status == status.Value)
                .Skip(skip)
                .Take(take)
                .ToList());

        public Task<int> CountAsync(Guid? userId = null, TarotPlusSessionStatus? status = null, CancellationToken ct = default) =>
            Task.FromResult(Items
                .Where(x => !userId.HasValue || x.UserId == userId.Value)
                .Count(x => !status.HasValue || x.Status == status.Value));

        public Task<int> CountPaidOrLaterAsync(CancellationToken ct = default) => Task.FromResult(Items.Count);

        public Task<int> CountReportsReadyAsync(CancellationToken ct = default) =>
            Task.FromResult(Items.Count(x => x.Status is TarotPlusSessionStatus.ReportReady or TarotPlusSessionStatus.Completed));

        public Task<int> CountSinceAsync(DateTime sinceUtc, CancellationToken ct = default) =>
            Task.FromResult(Items.Count(x => x.CreatedAt >= sinceUtc));

        public Task UpdateAsync(TarotPlusSession session, CancellationToken ct = default) => Task.CompletedTask;
    }
}
