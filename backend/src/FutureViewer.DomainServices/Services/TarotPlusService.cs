using System.Text.Json;
using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Exceptions;
using FutureViewer.DomainServices.Interfaces;
using FutureViewer.DomainServices.TarotPlus;
using Microsoft.Extensions.Logging;

namespace FutureViewer.DomainServices.Services;

public sealed class TarotPlusService
{
    public const decimal SessionPriceRub = 100m;
    public const int MinIntakeAnswers = 5;
    public const int MaxIntakeAnswers = 9;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] DefaultQuestions =
    {
        "Что в этой теме сейчас вызывает самое сильное напряжение?",
        "Какой результат ты считаешь реалистичным и желанным?",
        "Что уже пробовал(а), и почему это не сработало полностью?",
        "Какая внутренняя или внешняя граница здесь важна?",
        "Какой ресурс у тебя уже есть, но используется не полностью?",
        "Чего ты опасаешься, если ситуация начнёт меняться?",
        "По каким признакам через месяц ты поймёшь, что стало лучше?",
        "Какой первый шаг кажется маленьким, но честным?"
    };

    private readonly ITarotPlusSessionRepository _sessions;
    private readonly IUserRepository _users;
    private readonly IPaymentProvider _payments;
    private readonly IUnitOfWork _uow;
    private readonly CardDeckService _deck;
    private readonly ITarotPlusAI _ai;
    private readonly ILogger<TarotPlusService> _logger;

    public TarotPlusService(
        ITarotPlusSessionRepository sessions,
        IUserRepository users,
        IPaymentProvider payments,
        IUnitOfWork uow,
        CardDeckService deck,
        ITarotPlusAI ai,
        ILogger<TarotPlusService> logger)
    {
        _sessions = sessions;
        _users = users;
        _payments = payments;
        _uow = uow;
        _deck = deck;
        _ai = ai;
        _logger = logger;
    }

    public async Task<TarotPlusPreviewDto> CreatePreviewAsync(
        CreateTarotPlusPreviewRequest request,
        Guid userId,
        CancellationToken ct = default)
    {
        await EnsureUserExistsAsync(userId, ct);
        var coreRequest = RequiredText(request.CoreRequest, "coreRequest", minLength: 8, maxLength: 4000);
        var mainSphere = RequiredText(request.MainSphere, "mainSphere", minLength: 3, maxLength: 1000);
        var desiredOutcome = RequiredText(request.DesiredOutcome, "desiredOutcome", minLength: 5, maxLength: 1000);

        var answers = new List<TarotPlusAnswerDto>
        {
            NewAnswer("preview", "Что сейчас больше всего хочется разобрать?", coreRequest),
            NewAnswer("preview", "Какая сфера главная?", mainSphere),
            NewAnswer("preview", "Что хочешь получить после разбора?", desiredOutcome)
        };

        var session = new TarotPlusSession
        {
            UserId = userId,
            CoreRequest = coreRequest,
            AnswersJson = Serialize(answers),
            PriceRub = SessionPriceRub
        };

        TarotPlusRouteResult route;
        try
        {
            route = await _ai.RouteAsync(BuildInterviewContext(session, answers), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Tarot+ preview AI failed for user {UserId}; using fallback preview", userId);
            route = new TarotPlusRouteResult
            {
                Route = TarotPlusRoute.GeneralLife,
                PreviewText = FallbackPreview(coreRequest),
                SafetyFlags = new[] { "ai_preview_fallback" }
            };
        }

        session.Status = TarotPlusSessionStatus.PreviewReady;
        session.Route = route.Route;
        session.PreviewText = Trim(route.PreviewText, 4000);
        session.SafetyFlagsJson = Serialize(route.SafetyFlags);
        session.AiModel = _ai.Model;
        session.UpdatedAt = DateTime.UtcNow;

        await _sessions.AddAsync(session, ct);

        return new TarotPlusPreviewDto
        {
            Session = Map(session),
            PreviewText = session.PreviewText ?? string.Empty,
            Route = session.Route,
            RouteLabel = RouteLabel(session.Route)
        };
    }

    public async Task<PaymentCreationDto> CreatePaymentAsync(
        Guid sessionId,
        Guid userId,
        CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new UnauthorizedException("User not found");
        var session = await GetOwnedSessionAsync(sessionId, userId, ct);
        EnsureNotExpired(session);

        if (session.Status is TarotPlusSessionStatus.Paid
            or TarotPlusSessionStatus.Intake
            or TarotPlusSessionStatus.CardsDrawn
            or TarotPlusSessionStatus.ReportGenerating
            or TarotPlusSessionStatus.ReportReady
            or TarotPlusSessionStatus.Completed)
        {
            throw new ConflictException("Tarot+ session is already paid");
        }

        if (user.TarotPlusCredits > 0)
        {
            return await _uow.ExecuteInTransactionAsync(async innerCt =>
            {
                user.TarotPlusCredits -= 1;
                session.Status = TarotPlusSessionStatus.Paid;
                session.PaymentId = $"credit:{session.Id:N}";
                session.PaidAt = DateTime.UtcNow;
                session.PriceRub = 0m;
                session.UpdatedAt = DateTime.UtcNow;

                await _users.UpdateAsync(user, innerCt);
                await _sessions.UpdateAsync(session, innerCt);

                return new PaymentCreationDto
                {
                    PaymentId = session.PaymentId,
                    ConfirmationUrl = $"/tarot-plus/{session.Id}",
                    Status = "succeeded"
                };
            }, ct);
        }

        var result = await _payments.CreatePaymentAsync(new PaymentCreateRequest
        {
            UserId = user.Id,
            UserEmail = user.Email,
            ProductType = PaymentProductType.TarotPlusSession,
            AmountRub = SessionPriceRub,
            Description = "Tarot+ Жизненный компас",
            TarotPlusSessionId = session.Id,
            ReturnPath = $"/tarot-plus/{session.Id}"
        }, ct);

        session.Status = TarotPlusSessionStatus.PaymentPending;
        session.PaymentId = result.PaymentId;
        session.PriceRub = SessionPriceRub;
        session.UpdatedAt = DateTime.UtcNow;
        await _sessions.UpdateAsync(session, ct);

        return new PaymentCreationDto
        {
            PaymentId = result.PaymentId,
            ConfirmationUrl = result.ConfirmationUrl,
            Status = result.Status
        };
    }

    public async Task<bool> ProcessPaymentSucceededAsync(
        PaymentVerification verified,
        PaymentWebhookEvent evt,
        CancellationToken ct = default)
    {
        if (!verified.Paid) return false;
        if (!string.Equals(verified.Status, "succeeded", StringComparison.OrdinalIgnoreCase)) return false;
        if (verified.ProductType != PaymentProductType.TarotPlusSession) return false;

        var sessionId = verified.TarotPlusSessionId;
        if (sessionId is null) return false;

        var session = await _sessions.GetByIdAsync(sessionId.Value, ct);
        if (session is null) return false;
        if (verified.UserId is { } verifiedUserId && verifiedUserId != session.UserId) return false;

        if (session.Status is TarotPlusSessionStatus.ReportReady
            or TarotPlusSessionStatus.Completed
            or TarotPlusSessionStatus.Paid
            or TarotPlusSessionStatus.Intake
            or TarotPlusSessionStatus.CardsDrawn
            or TarotPlusSessionStatus.ReportGenerating)
        {
            return true;
        }

        session.Status = TarotPlusSessionStatus.Paid;
        session.PaymentId = verified.PaymentId;
        session.PaidAt = DateTime.UtcNow;
        session.PriceRub = SessionPriceRub;
        session.UpdatedAt = DateTime.UtcNow;
        await _sessions.UpdateAsync(session, ct);
        return true;
    }

    public async Task<TarotPlusSessionDto> AddAnswerAsync(
        Guid sessionId,
        Guid userId,
        AddTarotPlusAnswerRequest request,
        CancellationToken ct = default)
    {
        var session = await GetOwnedSessionAsync(sessionId, userId, ct);
        EnsureNotExpired(session);

        if (session.Status is not (TarotPlusSessionStatus.Paid or TarotPlusSessionStatus.Intake))
            throw new ConflictException("Tarot+ session must be paid before intake answers");

        var question = RequiredText(request.Question, "question", minLength: 3, maxLength: 1000);
        var answer = RequiredText(request.Answer, "answer", minLength: 3, maxLength: 4000);
        var answers = ReadAnswers(session);
        var intakeCount = CountIntakeAnswers(answers);
        if (intakeCount >= MaxIntakeAnswers)
            throw new ConflictException("Tarot+ intake already has enough answers");

        answers.Add(NewAnswer("intake", question, answer));
        session.AnswersJson = Serialize(answers);
        session.Status = TarotPlusSessionStatus.Intake;
        session.UpdatedAt = DateTime.UtcNow;
        await _sessions.UpdateAsync(session, ct);
        return Map(session);
    }

    public async Task<TarotPlusNextStepDto> GetNextStepAsync(
        Guid sessionId,
        Guid userId,
        CancellationToken ct = default)
    {
        var session = await GetOwnedSessionAsync(sessionId, userId, ct);
        EnsureNotExpired(session);
        var answers = ReadAnswers(session);
        var intakeCount = CountIntakeAnswers(answers);

        if (session.Status is TarotPlusSessionStatus.ReportReady or TarotPlusSessionStatus.Completed)
        {
            return new TarotPlusNextStepDto
            {
                Status = session.Status,
                Question = null,
                CanGenerateReport = false,
                AnswerCount = intakeCount,
                RequiredAnswers = MinIntakeAnswers,
                MaxAnswers = MaxIntakeAnswers
            };
        }

        if (session.Status is not (TarotPlusSessionStatus.Paid or TarotPlusSessionStatus.Intake))
        {
            return new TarotPlusNextStepDto
            {
                Status = session.Status,
                Question = null,
                CanGenerateReport = false,
                AnswerCount = intakeCount,
                RequiredAnswers = MinIntakeAnswers,
                MaxAnswers = MaxIntakeAnswers
            };
        }

        if (intakeCount < MinIntakeAnswers)
            return NextStep(session.Status, DefaultQuestions[intakeCount], intakeCount, canGenerateReport: false);

        if (intakeCount >= MaxIntakeAnswers)
            return NextStep(session.Status, null, intakeCount, canGenerateReport: true);

        TarotPlusQuestionResult aiQuestion;
        try
        {
            aiQuestion = await _ai.NextQuestionAsync(BuildInterviewContext(session, answers), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Tarot+ next question AI failed for session {SessionId}; allowing report generation", session.Id);
            return NextStep(session.Status, null, intakeCount, canGenerateReport: true);
        }

        var safetyFlags = MergeSafetyFlags(ReadSafetyFlags(session), aiQuestion.SafetyFlags);
        session.SafetyFlagsJson = Serialize(safetyFlags);
        session.UpdatedAt = DateTime.UtcNow;
        await _sessions.UpdateAsync(session, ct);

        return NextStep(
            session.Status,
            aiQuestion.ReadyForReport ? null : aiQuestion.Question ?? DefaultQuestions[Math.Min(intakeCount, DefaultQuestions.Length - 1)],
            intakeCount,
            canGenerateReport: aiQuestion.ReadyForReport || intakeCount >= MinIntakeAnswers);
    }

    public async Task<TarotPlusReportDto> GenerateReportAsync(
        Guid sessionId,
        Guid userId,
        CancellationToken ct = default)
    {
        var session = await GetOwnedSessionAsync(sessionId, userId, ct);
        EnsureNotExpired(session);
        var answers = ReadAnswers(session);
        var intakeCount = CountIntakeAnswers(answers);

        if (intakeCount < MinIntakeAnswers)
            throw new ConflictException($"At least {MinIntakeAnswers} intake answers are required");
        if (session.Status is not (TarotPlusSessionStatus.Paid
            or TarotPlusSessionStatus.Intake
            or TarotPlusSessionStatus.CardsDrawn
            or TarotPlusSessionStatus.ReportGenerating))
            throw new ConflictException("Tarot+ report cannot be generated for this session status");

        var drawn = ReadDrawnSpreads(session);
        if (drawn.Count == 0)
        {
            drawn = await DrawSpreadsAsync(session.Route, ct);
            session.SelectedSpreadsJson = Serialize(drawn.Select(x => new { x.SpreadId, x.SpreadName }).ToList());
            session.DrawnCardsJson = Serialize(drawn);
            session.Status = TarotPlusSessionStatus.CardsDrawn;
            session.UpdatedAt = DateTime.UtcNow;
            await _sessions.UpdateAsync(session, ct);
        }

        session.Status = TarotPlusSessionStatus.ReportGenerating;
        session.UpdatedAt = DateTime.UtcNow;
        await _sessions.UpdateAsync(session, ct);

        try
        {
            var result = await _ai.GenerateReportAsync(new TarotPlusReportContext
            {
                CoreRequest = session.CoreRequest,
                Route = session.Route,
                Answers = answers,
                DrawnSpreads = drawn,
                SafetyFlags = ReadSafetyFlags(session)
            }, ct);

            session.ReportMarkdown = result.ReportMarkdown;
            session.AiModel = _ai.Model;
            session.Status = TarotPlusSessionStatus.ReportReady;
            session.UpdatedAt = DateTime.UtcNow;
            await _sessions.UpdateAsync(session, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Tarot+ report AI failed for session {SessionId}; using card-based fallback report", session.Id);
            session.ReportMarkdown = BuildFallbackReportMarkdown(session, answers, drawn);
            session.AiModel = $"{_ai.Model}:fallback";
            session.SafetyFlagsJson = Serialize(MergeSafetyFlags(ReadSafetyFlags(session), new[] { "ai_report_fallback" }));
            session.Status = TarotPlusSessionStatus.ReportReady;
            session.UpdatedAt = DateTime.UtcNow;
            await _sessions.UpdateAsync(session, CancellationToken.None);
        }

        return new TarotPlusReportDto
        {
            Session = Map(session),
            ReportMarkdown = session.ReportMarkdown ?? string.Empty,
            DrawnSpreads = drawn
        };
    }

    public async Task<TarotPlusFollowUpDto> AskFollowUpAsync(
        Guid sessionId,
        Guid userId,
        TarotPlusFollowUpRequest request,
        CancellationToken ct = default)
    {
        var session = await GetOwnedSessionAsync(sessionId, userId, ct);
        EnsureNotExpired(session);

        if (session.Status != TarotPlusSessionStatus.ReportReady)
            throw new ConflictException("Follow-up questions are available only after the report is ready");
        if (session.FollowUpsLeft <= 0)
            throw new ConflictException("No follow-up questions left");
        if (string.IsNullOrWhiteSpace(session.ReportMarkdown))
            throw new ConflictException("Tarot+ report is empty");

        var question = RequiredText(request.Question, "question", minLength: 3, maxLength: 1000);
        var followUps = ReadFollowUps(session);
        var result = await _ai.AskFollowUpAsync(new TarotPlusFollowUpContext
        {
            CoreRequest = session.CoreRequest,
            Route = session.Route,
            ReportMarkdown = session.ReportMarkdown,
            DrawnSpreads = ReadDrawnSpreads(session),
            PreviousFollowUps = followUps,
            Question = question
        }, ct);

        followUps.Add(new TarotPlusFollowUpItemDto
        {
            Question = question,
            AnswerMarkdown = result.AnswerMarkdown,
            CreatedAt = DateTime.UtcNow
        });
        session.FollowUpsJson = Serialize(followUps);
        session.FollowUpsLeft = Math.Max(0, session.FollowUpsLeft - 1);
        if (session.FollowUpsLeft == 0)
            session.Status = TarotPlusSessionStatus.Completed;
        session.UpdatedAt = DateTime.UtcNow;
        await _sessions.UpdateAsync(session, ct);

        return new TarotPlusFollowUpDto
        {
            Session = Map(session),
            AnswerMarkdown = result.AnswerMarkdown,
            FollowUpsLeft = session.FollowUpsLeft
        };
    }

    public async Task<IReadOnlyList<TarotPlusSessionListItemDto>> GetHistoryAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var sessions = await _sessions.GetHistoryAsync(userId, take: 20, ct);
        return sessions.Select(MapListItem).ToList();
    }

    public async Task<TarotPlusSessionDto> GetAsync(
        Guid sessionId,
        Guid userId,
        CancellationToken ct = default)
    {
        var session = await GetOwnedSessionAsync(sessionId, userId, ct);
        EnsureNotExpired(session);
        return Map(session);
    }

    private async Task EnsureUserExistsAsync(Guid userId, CancellationToken ct)
    {
        _ = await _users.GetByIdAsync(userId, ct)
            ?? throw new UnauthorizedException("User not found");
    }

    private async Task<TarotPlusSession> GetOwnedSessionAsync(Guid sessionId, Guid userId, CancellationToken ct)
    {
        var session = await _sessions.GetByIdAsync(sessionId, ct)
            ?? throw new NotFoundException($"Tarot+ session {sessionId} not found");
        if (session.UserId != userId)
            throw new NotFoundException($"Tarot+ session {sessionId} not found");
        return session;
    }

    private static void EnsureNotExpired(TarotPlusSession session)
    {
        if (session.ExpiresAt <= DateTime.UtcNow || session.Status == TarotPlusSessionStatus.Expired)
            throw new ConflictException("Tarot+ session is expired");
        if (session.Status == TarotPlusSessionStatus.Cancelled)
            throw new ConflictException("Tarot+ session is cancelled");
    }

    private async Task<List<TarotPlusDrawnSpreadDto>> DrawSpreadsAsync(TarotPlusRoute route, CancellationToken ct)
    {
        var spreadDefinitions = TarotPlusSpreadCatalog.SelectFor(route);
        var totalCards = spreadDefinitions.Sum(x => x.CardCount);
        var drawnCards = await _deck.DrawAsync(totalCards, ct);
        var cursor = 0;
        var result = new List<TarotPlusDrawnSpreadDto>();

        foreach (var spread in spreadDefinitions)
        {
            var cards = new List<TarotPlusDrawnCardDto>();
            for (var position = 0; position < spread.CardCount; position++)
            {
                var (card, isReversed) = drawnCards[cursor++];
                cards.Add(new TarotPlusDrawnCardDto
                {
                    Position = position,
                    PositionName = spread.Positions[position],
                    CardId = card.Id,
                    CardName = card.Name,
                    ImagePath = card.ImagePath,
                    IsReversed = isReversed,
                    Meaning = isReversed ? card.DescriptionReversed : card.DescriptionUpright
                });
            }

            result.Add(new TarotPlusDrawnSpreadDto
            {
                SpreadId = spread.Id,
                SpreadName = spread.Name,
                Cards = cards
            });
        }

        return result;
    }

    private static TarotPlusNextStepDto NextStep(
        TarotPlusSessionStatus status,
        string? question,
        int intakeCount,
        bool canGenerateReport) =>
        new()
        {
            Status = status,
            Question = question,
            CanGenerateReport = canGenerateReport,
            AnswerCount = intakeCount,
            RequiredAnswers = MinIntakeAnswers,
            MaxAnswers = MaxIntakeAnswers
        };

    private static TarotPlusInterviewContext BuildInterviewContext(
        TarotPlusSession session,
        IReadOnlyList<TarotPlusAnswerDto> answers) =>
        new()
        {
            CoreRequest = session.CoreRequest,
            Answers = answers,
            Route = session.Route,
            SafetyFlags = ReadSafetyFlags(session),
            IntakeAnswerCount = CountIntakeAnswers(answers)
        };

    private static TarotPlusAnswerDto NewAnswer(string kind, string question, string answer) =>
        new()
        {
            Kind = kind,
            Question = Trim(question, 1000),
            Answer = Trim(answer, 4000),
            CreatedAt = DateTime.UtcNow
        };

    private static TarotPlusSessionDto Map(TarotPlusSession session)
    {
        var answers = ReadAnswers(session);
        return new TarotPlusSessionDto
        {
            Id = session.Id,
            Status = session.Status,
            Route = session.Route,
            RouteLabel = RouteLabel(session.Route),
            CoreRequest = session.CoreRequest,
            PreviewText = session.PreviewText,
            ReportMarkdown = session.ReportMarkdown,
            FollowUpsLeft = session.FollowUpsLeft,
            PriceRub = session.PriceRub,
            PaidAt = session.PaidAt,
            CreatedAt = session.CreatedAt,
            UpdatedAt = session.UpdatedAt,
            ExpiresAt = session.ExpiresAt,
            AnswerCount = answers.Count,
            IntakeAnswerCount = CountIntakeAnswers(answers),
            Answers = answers,
            DrawnSpreads = ReadDrawnSpreads(session),
            FollowUps = ReadFollowUps(session)
        };
    }

    private static TarotPlusSessionListItemDto MapListItem(TarotPlusSession session) =>
        new()
        {
            Id = session.Id,
            Status = session.Status,
            Route = session.Route,
            RouteLabel = RouteLabel(session.Route),
            CoreRequest = session.CoreRequest,
            PriceRub = session.PriceRub,
            PaidAt = session.PaidAt,
            CreatedAt = session.CreatedAt,
            ExpiresAt = session.ExpiresAt
        };

    public static string RouteLabel(TarotPlusRoute route) =>
        route switch
        {
            TarotPlusRoute.Relationship => "Отношения",
            TarotPlusRoute.Career => "Карьера",
            TarotPlusRoute.Money => "Деньги",
            TarotPlusRoute.Decision => "Выбор",
            TarotPlusRoute.SelfIdentity => "Самоопределение",
            TarotPlusRoute.Family => "Семья",
            TarotPlusRoute.ResourceState => "Ресурсное состояние",
            TarotPlusRoute.SafetySensitive => "Чувствительная тема",
            TarotPlusRoute.GeneralLife => "Жизненный обзор",
            _ => "Жизненный обзор"
        };

    private static List<TarotPlusAnswerDto> ReadAnswers(TarotPlusSession session) =>
        Deserialize<List<TarotPlusAnswerDto>>(session.AnswersJson);

    private static List<TarotPlusDrawnSpreadDto> ReadDrawnSpreads(TarotPlusSession session) =>
        Deserialize<List<TarotPlusDrawnSpreadDto>>(session.DrawnCardsJson);

    private static List<TarotPlusFollowUpItemDto> ReadFollowUps(TarotPlusSession session) =>
        Deserialize<List<TarotPlusFollowUpItemDto>>(session.FollowUpsJson);

    private static List<string> ReadSafetyFlags(TarotPlusSession session) =>
        Deserialize<List<string>>(session.SafetyFlagsJson);

    private static List<string> MergeSafetyFlags(IReadOnlyList<string> existing, IReadOnlyList<string> next) =>
        existing.Concat(next)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();

    private static int CountIntakeAnswers(IReadOnlyList<TarotPlusAnswerDto> answers) =>
        answers.Count(x => string.Equals(x.Kind, "intake", StringComparison.OrdinalIgnoreCase));

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

    private static T Deserialize<T>(string? json) where T : new()
    {
        if (string.IsNullOrWhiteSpace(json))
            return new T();

        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? new T();
        }
        catch (JsonException)
        {
            return new T();
        }
    }

    private static string Trim(string value, int maxLength)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string FallbackPreview(string coreRequest) =>
        $"Запрос «{Trim(coreRequest, 180)}» подходит для глубокого Tarot+ разбора. " +
        "После оплаты я задам несколько уточняющих вопросов, вытяну несколько раскладов и соберу практический жизненный компас.";

    private static string BuildFallbackReportMarkdown(
        TarotPlusSession session,
        IReadOnlyList<TarotPlusAnswerDto> answers,
        IReadOnlyList<TarotPlusDrawnSpreadDto> drawn)
    {
        var previewAnswers = answers
            .Where(x => string.Equals(x.Kind, "preview", StringComparison.OrdinalIgnoreCase))
            .Take(3)
            .ToList();
        var intakeAnswers = answers
            .Where(x => string.Equals(x.Kind, "intake", StringComparison.OrdinalIgnoreCase))
            .Take(MaxIntakeAnswers)
            .ToList();
        var allCards = drawn.SelectMany(x => x.Cards.Select(card => new { Spread = x, Card = card })).ToList();
        var reversedCards = allCards.Where(x => x.Card.IsReversed).Take(5).ToList();
        var resourceCards = allCards.Where(x => !x.Card.IsReversed).Take(5).ToList();

        var lines = new List<string>
        {
            "# Жизненный компас",
            "",
            "## Главная тема",
            "",
            $"Запрос: **{session.CoreRequest}**.",
            "",
            $"Ветка анализа: **{RouteLabel(session.Route)}**. Отчёт собран по вашим ответам и уже вытянутым картам, без повторного вытягивания.",
            ""
        };

        if (previewAnswers.Count > 0 || intakeAnswers.Count > 0)
        {
            lines.Add("## Контекст из ответов");
            lines.Add("");
            foreach (var answer in previewAnswers.Concat(intakeAnswers).Take(7))
                lines.Add($"- **{answer.Question}** — {answer.Answer}");
            lines.Add("");
        }

        lines.Add("## Что показывают карты");
        lines.Add("");
        foreach (var spread in drawn)
        {
            lines.Add($"### {spread.SpreadName}");
            lines.Add("");
            foreach (var card in spread.Cards)
            {
                var reversed = card.IsReversed ? " · перевёрнутая" : "";
                lines.Add($"- **{card.PositionName}: {card.CardName}{reversed}** — {card.Meaning}");
            }
            lines.Add("");
        }

        lines.Add("## Слепые зоны и ресурсы");
        lines.Add("");
        if (reversedCards.Count > 0)
        {
            lines.Add("Слепые зоны сейчас лучше проверять через карты, которые вышли перевёрнутыми:");
            lines.Add("");
            foreach (var item in reversedCards)
                lines.Add($"- **{item.Card.CardName}** в позиции «{item.Card.PositionName}»: {item.Card.Meaning}");
            lines.Add("");
        }
        if (resourceCards.Count > 0)
        {
            lines.Add("Ресурсы, на которые можно опереться:");
            lines.Add("");
            foreach (var item in resourceCards)
                lines.Add($"- **{item.Card.CardName}** в позиции «{item.Card.PositionName}»: {item.Card.Meaning}");
            lines.Add("");
        }

        lines.AddRange(new[]
        {
            "## Рекомендации на 7 дней",
            "",
            "- Выберите один наблюдаемый шаг по главной теме и выполните его без попытки решить всё сразу.",
            "- Отметьте, какая карта из расклада точнее всего описывает текущее напряжение.",
            "- Запишите один признак прогресса, который можно проверить через неделю.",
            "",
            "## Рекомендации на 30 дней",
            "",
            "- Вернитесь к позициям расклада, где вышли перевёрнутые карты, и проверьте, что из этого уже стало яснее.",
            "- Сведите решения к двум-трём практическим действиям, а не к общему ожиданию перемен.",
            "- Отдельно отслеживайте ресурсные карты: они показывают, через что проще восстанавливать устойчивость.",
            "",
            "## Рекомендации на 90 дней",
            "",
            "- Смотрите не только на результат, но и на повторяющийся паттерн: что снова требует честности, границ или действия.",
            "- Если тема остаётся тяжёлой, используйте расклад как карту наблюдений, а важные медицинские, юридические или финансовые решения принимайте с профильными специалистами.",
            "",
            "## Вопросы для самопроверки",
            "",
            "- Что в раскладе уже подтверждается конкретными событиями?",
            "- Какая позиция вызывает сопротивление, но выглядит полезной?",
            "- Какой маленький шаг можно сделать без риска и без давления на себя?"
        });

        return string.Join('\n', lines);
    }

    private static string RequiredText(string? value, string field, int minLength, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new DomainException($"{field} is required");
        if (trimmed.Length < minLength)
            throw new DomainException($"{field} must be at least {minLength} characters");
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
