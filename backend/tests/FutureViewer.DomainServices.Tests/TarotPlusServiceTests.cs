using FluentAssertions;
using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Exceptions;
using FutureViewer.DomainServices.Interfaces;
using FutureViewer.DomainServices.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FutureViewer.DomainServices.Tests;

public sealed class TarotPlusServiceTests
{
    [Fact]
    public async Task CreatePreviewAsync_creates_session_with_preview_ready()
    {
        var user = NewUser();
        var repo = new InMemoryTarotPlusSessionRepository();
        var sut = CreateSut(repo, user: user);

        var result = await sut.CreatePreviewAsync(new CreateTarotPlusPreviewRequest
        {
            CoreRequest = "Хочу понять следующий жизненный шаг",
            MainSphere = "работа",
            DesiredOutcome = "получить ясный план"
        }, user.Id);

        result.Session.Status.Should().Be(TarotPlusSessionStatus.PreviewReady);
        result.Session.PriceRub.Should().Be(100m);
        result.Session.Answers.Should().HaveCount(3);
        repo.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task CreatePreviewAsync_rejects_blank_inputs()
    {
        var user = NewUser();
        var sut = CreateSut(new InMemoryTarotPlusSessionRepository(), user: user);

        var act = () => sut.CreatePreviewAsync(new CreateTarotPlusPreviewRequest
        {
            CoreRequest = "   ",
            MainSphere = "работа",
            DesiredOutcome = "получить ясный план"
        }, user.Id);

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task CreatePaymentAsync_sets_payment_pending_and_price_100()
    {
        var user = NewUser();
        var repo = new InMemoryTarotPlusSessionRepository();
        var session = await repo.AddAsync(NewSession(user.Id, TarotPlusSessionStatus.PreviewReady));
        var payments = new Mock<IPaymentProvider>();
        payments.Setup(x => x.CreatePaymentAsync(
                It.Is<PaymentCreateRequest>(r =>
                    r.ProductType == PaymentProductType.TarotPlusSession &&
                    r.AmountRub == 100m &&
                    r.TarotPlusSessionId == session.Id),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentCreationResult
            {
                PaymentId = "tp-pay-1",
                ConfirmationUrl = "https://pay.example/tp",
                Status = "pending"
            });
        var sut = CreateSut(repo, user: user, payments: payments.Object);

        var result = await sut.CreatePaymentAsync(session.Id, user.Id);

        result.PaymentId.Should().Be("tp-pay-1");
        session.Status.Should().Be(TarotPlusSessionStatus.PaymentPending);
        session.PaymentId.Should().Be("tp-pay-1");
        session.PriceRub.Should().Be(100m);
    }

    [Fact]
    public async Task CreatePaymentAsync_consumes_tarot_plus_credit_without_provider_payment()
    {
        var user = NewUser();
        user.TarotPlusCredits = 1;
        var users = UserRepoFor(user);
        var repo = new InMemoryTarotPlusSessionRepository();
        var session = await repo.AddAsync(NewSession(user.Id, TarotPlusSessionStatus.PreviewReady));
        var payments = new Mock<IPaymentProvider>();
        var sut = CreateSut(repo, user: user, users: users.Object, payments: payments.Object);

        var result = await sut.CreatePaymentAsync(session.Id, user.Id);

        result.Status.Should().Be("succeeded");
        result.ConfirmationUrl.Should().Be($"/tarot-plus/{session.Id}");
        user.TarotPlusCredits.Should().Be(0);
        session.Status.Should().Be(TarotPlusSessionStatus.Paid);
        session.PaymentId.Should().StartWith("credit:");
        session.PriceRub.Should().Be(0m);
        payments.Verify(x => x.CreatePaymentAsync(It.IsAny<PaymentCreateRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        users.Verify(x => x.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddAnswerAsync_requires_paid_session()
    {
        var user = NewUser();
        var repo = new InMemoryTarotPlusSessionRepository();
        var session = await repo.AddAsync(NewSession(user.Id, TarotPlusSessionStatus.PreviewReady));
        var sut = CreateSut(repo, user: user);

        var act = () => sut.AddAnswerAsync(session.Id, user.Id, new AddTarotPlusAnswerRequest
        {
            Question = "Что важно?",
            Answer = "Ответ"
        });

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task AddAnswerAsync_updates_answers_json()
    {
        var user = NewUser();
        var repo = new InMemoryTarotPlusSessionRepository();
        var session = await repo.AddAsync(NewSession(user.Id, TarotPlusSessionStatus.Paid));
        var sut = CreateSut(repo, user: user);

        var result = await sut.AddAnswerAsync(session.Id, user.Id, new AddTarotPlusAnswerRequest
        {
            Question = "Что важно?",
            Answer = "Больше ясности"
        });

        result.IntakeAnswerCount.Should().Be(1);
        result.Answers.Should().Contain(a => a.Kind == "intake" && a.Answer == "Больше ясности");
        session.Status.Should().Be(TarotPlusSessionStatus.Intake);
    }

    [Fact]
    public async Task AddAnswerAsync_rejects_blank_answer()
    {
        var user = NewUser();
        var repo = new InMemoryTarotPlusSessionRepository();
        var session = await repo.AddAsync(NewSession(user.Id, TarotPlusSessionStatus.Paid));
        var sut = CreateSut(repo, user: user);

        var act = () => sut.AddAnswerAsync(session.Id, user.Id, new AddTarotPlusAnswerRequest
        {
            Question = "Что важно?",
            Answer = "  "
        });

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task GenerateReportAsync_sets_report_ready_and_draws_cards_once()
    {
        var user = NewUser();
        var repo = new InMemoryTarotPlusSessionRepository();
        var session = NewSession(user.Id, TarotPlusSessionStatus.Intake);
        for (var i = 0; i < TarotPlusService.MinIntakeAnswers; i++)
            session.AnswersJson = AddAnswerJson(session.AnswersJson, $"Q{i}", $"A{i}");
        await repo.AddAsync(session);
        var sut = CreateSut(repo, user: user);

        var result = await sut.GenerateReportAsync(session.Id, user.Id);

        result.Session.Status.Should().Be(TarotPlusSessionStatus.ReportReady);
        result.ReportMarkdown.Should().Contain("Жизненный компас");
        result.DrawnSpreads.Should().HaveCount(3);
        result.DrawnSpreads.SelectMany(s => s.Cards).Should().NotBeEmpty();
    }

    [Fact]
    public async Task GenerateReportAsync_uses_fallback_report_when_ai_fails()
    {
        var user = NewUser();
        var repo = new InMemoryTarotPlusSessionRepository();
        var session = NewSession(user.Id, TarotPlusSessionStatus.Intake);
        for (var i = 0; i < TarotPlusService.MinIntakeAnswers; i++)
            session.AnswersJson = AddAnswerJson(session.AnswersJson, $"Q{i}", $"A{i}");
        await repo.AddAsync(session);
        var sut = CreateSut(repo, user: user, ai: new FailingTarotPlusAI());

        var result = await sut.GenerateReportAsync(session.Id, user.Id);

        result.Session.Status.Should().Be(TarotPlusSessionStatus.ReportReady);
        result.ReportMarkdown.Should().Contain("Что показывают карты");
        result.ReportMarkdown.Should().Contain("Рекомендации на 7 дней");
        session.AiModel.Should().Be("failing-tarot-plus:fallback");
    }

    [Fact]
    public async Task AskFollowUpAsync_decrements_followups_left_and_blocks_when_empty()
    {
        var user = NewUser();
        var repo = new InMemoryTarotPlusSessionRepository();
        var session = NewSession(user.Id, TarotPlusSessionStatus.ReportReady);
        session.ReportMarkdown = "# Report";
        session.FollowUpsLeft = 1;
        await repo.AddAsync(session);
        var sut = CreateSut(repo, user: user);

        var first = await sut.AskFollowUpAsync(session.Id, user.Id, new TarotPlusFollowUpRequest { Question = "Что делать?" });
        first.FollowUpsLeft.Should().Be(0);
        first.Session.Status.Should().Be(TarotPlusSessionStatus.Completed);

        var act = () => sut.AskFollowUpAsync(session.Id, user.Id, new TarotPlusFollowUpRequest { Question = "Ещё?" });
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task AskFollowUpAsync_rejects_blank_question()
    {
        var user = NewUser();
        var repo = new InMemoryTarotPlusSessionRepository();
        var session = NewSession(user.Id, TarotPlusSessionStatus.ReportReady);
        session.ReportMarkdown = "# Report";
        await repo.AddAsync(session);
        var sut = CreateSut(repo, user: user);

        var act = () => sut.AskFollowUpAsync(session.Id, user.Id, new TarotPlusFollowUpRequest { Question = " " });

        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task GetAsync_blocks_other_user()
    {
        var user = NewUser();
        var repo = new InMemoryTarotPlusSessionRepository();
        var session = await repo.AddAsync(NewSession(user.Id, TarotPlusSessionStatus.Paid));
        var sut = CreateSut(repo, user: user);

        var act = () => sut.GetAsync(session.Id, Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    private static TarotPlusService CreateSut(
        InMemoryTarotPlusSessionRepository repo,
        User? user = null,
        IUserRepository? users = null,
        IPaymentProvider? payments = null,
        ITarotPlusAI? ai = null)
    {
        user ??= NewUser();
        users ??= UserRepoFor(user).Object;

        return new TarotPlusService(
            repo,
            users,
            payments ?? Mock.Of<IPaymentProvider>(),
            Uow(),
            new CardDeckService(new TestDeck()),
            ai ?? new FakeTarotPlusAI(),
            NullLogger<TarotPlusService>.Instance);
    }

    private static Mock<IUserRepository> UserRepoFor(User user)
    {
        var users = new Mock<IUserRepository>();
        users.Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        users.Setup(x => x.GetByIdAsync(It.Is<Guid>(id => id != user.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        return users;
    }

    private static IUnitOfWork Uow() => new PassThroughUnitOfWork();

    private static User NewUser() => new()
    {
        Id = Guid.NewGuid(),
        Email = "tarot-plus@example.com",
        PasswordHash = "hash"
    };

    private static TarotPlusSession NewSession(Guid userId, TarotPlusSessionStatus status) =>
        new()
        {
            UserId = userId,
            Status = status,
            Route = TarotPlusRoute.GeneralLife,
            CoreRequest = "Core request",
            PreviewText = "Preview",
            AnswersJson = "[]",
            PriceRub = 100m
        };

    private static string AddAnswerJson(string json, string question, string answer)
    {
        var list = System.Text.Json.JsonSerializer.Deserialize<List<TarotPlusAnswerDto>>(
            json,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)) ?? new();
        list.Add(new TarotPlusAnswerDto
        {
            Kind = "intake",
            Question = question,
            Answer = answer,
            CreatedAt = DateTime.UtcNow
        });
        return System.Text.Json.JsonSerializer.Serialize(
            list,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
    }

    private sealed class FakeTarotPlusAI : ITarotPlusAI
    {
        public string Model => "fake-tarot-plus";

        public Task<TarotPlusRouteResult> RouteAsync(TarotPlusInterviewContext context, CancellationToken ct = default) =>
            Task.FromResult(new TarotPlusRouteResult
            {
                Route = TarotPlusRoute.GeneralLife,
                PreviewText = "Preview text",
                SafetyFlags = Array.Empty<string>()
            });

        public Task<TarotPlusQuestionResult> NextQuestionAsync(TarotPlusInterviewContext context, CancellationToken ct = default) =>
            Task.FromResult(new TarotPlusQuestionResult
            {
                Question = "Следующий вопрос?",
                ReadyForReport = context.IntakeAnswerCount >= TarotPlusService.MinIntakeAnswers,
                SafetyFlags = Array.Empty<string>()
            });

        public Task<TarotPlusReportResult> GenerateReportAsync(TarotPlusReportContext context, CancellationToken ct = default) =>
            Task.FromResult(new TarotPlusReportResult { ReportMarkdown = "# Жизненный компас\n\nТестовый отчёт." });

        public Task<TarotPlusFollowUpResult> AskFollowUpAsync(TarotPlusFollowUpContext context, CancellationToken ct = default) =>
            Task.FromResult(new TarotPlusFollowUpResult { AnswerMarkdown = "Follow-up answer" });
    }

    private sealed class FailingTarotPlusAI : ITarotPlusAI
    {
        public string Model => "failing-tarot-plus";

        public Task<TarotPlusRouteResult> RouteAsync(TarotPlusInterviewContext context, CancellationToken ct = default) =>
            Task.FromResult(new TarotPlusRouteResult
            {
                Route = TarotPlusRoute.GeneralLife,
                PreviewText = "Preview text",
                SafetyFlags = Array.Empty<string>()
            });

        public Task<TarotPlusQuestionResult> NextQuestionAsync(TarotPlusInterviewContext context, CancellationToken ct = default) =>
            Task.FromResult(new TarotPlusQuestionResult
            {
                Question = "Следующий вопрос?",
                ReadyForReport = true,
                SafetyFlags = Array.Empty<string>()
            });

        public Task<TarotPlusReportResult> GenerateReportAsync(TarotPlusReportContext context, CancellationToken ct = default) =>
            throw new TimeoutException("AI timed out");

        public Task<TarotPlusFollowUpResult> AskFollowUpAsync(TarotPlusFollowUpContext context, CancellationToken ct = default) =>
            Task.FromResult(new TarotPlusFollowUpResult { AnswerMarkdown = "Follow-up answer" });
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

    private sealed class PassThroughUnitOfWork : IUnitOfWork
    {
        public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default) =>
            work(ct);
    }
}
