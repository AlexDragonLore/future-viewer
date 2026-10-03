using System.Runtime.CompilerServices;
using System.Text;
using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Exceptions;
using FutureViewer.DomainServices.Interfaces;
using Microsoft.Extensions.Logging;

namespace FutureViewer.DomainServices.Services;

public sealed class ReadingService
{
    private readonly IReadingRepository _repo;
    private readonly CardDeckService _deck;
    private readonly InterpretationService _interpreter;
    private readonly SubscriptionService _subscription;
    private readonly FeedbackService _feedback;
    private readonly PersonalizationService _personalization;
    private readonly IAIQuestionValidator _questionValidator;
    private readonly IAiPrivacyGateway _privacyGateway;
    private readonly IUserRepository _users;
    private readonly ILogger<ReadingService> _logger;

    public ReadingService(
        IReadingRepository repo,
        CardDeckService deck,
        InterpretationService interpreter,
        SubscriptionService subscription,
        FeedbackService feedback,
        PersonalizationService personalization,
        IAIQuestionValidator questionValidator,
        IAIMemoryExtractor memoryExtractor,
        IUserRepository users,
        ILogger<ReadingService> logger,
        IAiPrivacyGateway? privacyGateway = null)
    {
        _repo = repo;
        _deck = deck;
        _interpreter = interpreter;
        _subscription = subscription;
        _feedback = feedback;
        _personalization = personalization;
        _questionValidator = questionValidator;
        _ = memoryExtractor; // Kept in the constructor for binary/test compatibility; extraction is disabled.
        _users = users;
        _logger = logger;
        _privacyGateway = privacyGateway ?? new AiPrivacyGateway();
    }

    public Task<ReadingResult> CreateAsync(
        CreateReadingRequest request,
        Guid? userId,
        CancellationToken ct = default)
        => CreateCoreAsync(request, userId ?? throw new UnauthorizedException("Authentication required"), ct);

    public Task<ReadingResult> CreateGuestAsync(CreateReadingRequest request, CancellationToken ct = default)
    {
        if (request.SpreadType != SpreadType.SingleCard)
            throw new QuestionValidationException("guest_single_card_only", "Без регистрации можно открыть одну карту.");
        return CreateCoreAsync(request, null, ct);
    }

    public async Task<ReadingResult> UnlockGuestAsync(ReadingResult reading, Guid userId, CancellationToken ct = default)
    {
        if (!await _repo.AttachGuestAsync(reading.Id, userId, ct))
            throw new NotFoundException("Этот расклад уже недоступен. Начните новый расклад.");
        return reading;
    }

    private async Task<ReadingResult> CreateCoreAsync(
        CreateReadingRequest request,
        Guid? userId,
        CancellationToken ct)
    {
        var privacy = _privacyGateway.Prepare(request.Question, AiPrivacyOperation.TarotInterpretation);
        EnsureAllowed(privacy);
        var spread = Spread.From(request.SpreadType);
        var promptContext = userId is not null
            ? await PreparePromptContextAsync(request, userId, ct)
            : new UserPromptContext { Today = DateOnly.FromDateTime(DateTime.UtcNow), MemoryRules = [] };
        if (userId is { } uid)
            await _subscription.EnsureReadingAllowedAsync(uid, spread.Type, ct);
        var saveToHistory = userId is { } historyUserId
            && await IsHistoryStorageAllowedAsync(historyUserId, request.SaveToHistory, ct);
        var questionValidation = await ValidateQuestionAccessAsync(
            privacy.SafeText,
            request.QuestionWarningAcknowledged,
            ct);
        var drawn = await _deck.DrawAsync(spread.CardCount, ct);

        var cards = drawn
            .Select((x, idx) => new ReadingCard
            {
                CardId = x.Card.Id,
                Card = x.Card,
                Position = idx,
                IsReversed = x.IsReversed
            })
            .ToList();

        var variantNotes = await _deck.GetVariantNotesAsync(
            request.DeckType,
            cards.Select(c => c.CardId).ToList(),
            ct);

        var reading = new Reading
        {
            UserId = userId,
            SpreadType = spread.Type,
            Question = saveToHistory ? privacy.SafeText : string.Empty,
            SavedToHistory = saveToHistory,
            AiInterpretation = null,
            AiModel = _interpreter.Model,
            DeckType = request.DeckType,
            Cards = cards
        };

        // DB-first after deterministic minimization: no raw text crosses the AI boundary
        // before a local operational record exists. With history off the row contains no
        // question or interpretation and is used only for quota/accounting.
        await _repo.AddAsync(reading, ct);

        var interpretationQuestion = BuildQuestionForInterpretation(privacy.SafeText, questionValidation);
        var interpretation = await _interpreter.InterpretAsync(
            spread, interpretationQuestion, cards, request.DeckType, variantNotes, promptContext, ct);

        if (saveToHistory)
        {
            reading.AiInterpretation = interpretation.Text;
            reading.AiModel = interpretation.Model;
            await _repo.UpdateAsync(reading, ct);
        }

        if (saveToHistory && reading.UserId is not null)
        {
            try
            {
                await _feedback.ScheduleAsync(reading, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    "Failed to schedule feedback for reading {ReadingId}; errorType={ErrorType}",
                    reading.Id,
                    ex.GetType().Name);
            }
        }

        // AI memory remains disabled until a separately versioned personalization
        // consent is active and a privacy-reviewed structured schema is available.
        return Map(reading, spread, privacy.SafeText, interpretation.Text);
    }

    public async IAsyncEnumerable<ReadingStreamEvent> CreateStreamAsync(
        CreateReadingRequest request,
        Guid? userId,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var privacy = _privacyGateway.Prepare(request.Question, AiPrivacyOperation.TarotInterpretation);
        EnsureAllowed(privacy);
        var spread = Spread.From(request.SpreadType);
        var promptContext = await PreparePromptContextAsync(request, userId, ct);
        var uid = userId ?? throw new UnauthorizedException("Authentication required");
        await _subscription.EnsureReadingAllowedAsync(uid, spread.Type, ct);
        var saveToHistory = await IsHistoryStorageAllowedAsync(uid, request.SaveToHistory, ct);
        var questionValidation = await ValidateQuestionAccessAsync(
            privacy.SafeText,
            request.QuestionWarningAcknowledged,
            ct);
        var drawn = await _deck.DrawAsync(spread.CardCount, ct);

        var cards = drawn
            .Select((x, idx) => new ReadingCard
            {
                CardId = x.Card.Id,
                Card = x.Card,
                Position = idx,
                IsReversed = x.IsReversed
            })
            .ToList();

        var variantNotes = await _deck.GetVariantNotesAsync(
            request.DeckType,
            cards.Select(c => c.CardId).ToList(),
            ct);

        var reading = new Reading
        {
            UserId = userId,
            SpreadType = spread.Type,
            Question = saveToHistory ? privacy.SafeText : string.Empty,
            SavedToHistory = saveToHistory,
            AiInterpretation = null,
            AiModel = _interpreter.Model,
            DeckType = request.DeckType,
            Cards = cards
        };

        // Persist the minimized local record before external AI streaming begins.
        await _repo.AddAsync(reading, ct);
        yield return new ReadingStreamEvent.Cards(Map(reading, spread, privacy.SafeText, null));

        var sb = new StringBuilder();
        try
        {
            var interpretationQuestion = BuildQuestionForInterpretation(privacy.SafeText, questionValidation);
            await foreach (var delta in _interpreter.InterpretStreamAsync(
                spread, interpretationQuestion, cards, request.DeckType, variantNotes, promptContext, ct))
            {
                sb.Append(delta);
                yield return new ReadingStreamEvent.Chunk(delta);
            }
        }
        finally
        {
            if (saveToHistory && sb.Length > 0)
            {
                reading.AiInterpretation = sb.ToString();
                try
                {
                    await _repo.UpdateAsync(reading, CancellationToken.None);
                }
                catch
                {
                    // Best-effort persist; don't mask the original exception.
                }
            }

            if (saveToHistory && sb.Length > 0 && reading.UserId is not null)
            {
                try
                {
                    await _feedback.ScheduleAsync(reading, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        "Failed to schedule feedback for streaming reading {ReadingId}; errorType={ErrorType}",
                        reading.Id,
                        ex.GetType().Name);
                }
            }
        }

        yield return new ReadingStreamEvent.Done();
    }

    public async Task<ReadingResult> GetAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var reading = await _repo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Reading {id} not found");
        if (reading.UserId != userId)
            throw new NotFoundException($"Reading {id} not found");
        if (!reading.SavedToHistory || reading.DeletedFromHistoryAt is not null)
            throw new NotFoundException($"Reading {id} not found");
        var spread = Spread.From(reading.SpreadType);
        return Map(reading, spread);
    }

    public async Task<IReadOnlyList<ReadingResult>> GetHistoryAsync(Guid userId, CancellationToken ct = default)
    {
        var readings = await _repo.GetHistoryAsync(userId, take: 50, ct);
        return readings.Select(r => Map(r, Spread.From(r.SpreadType))).ToList();
    }

    public async Task DeleteFromHistoryAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var reading = await _repo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Reading {id} not found");
        if (reading.UserId != userId)
            throw new NotFoundException($"Reading {id} not found");

        if (reading.DeletedFromHistoryAt is not null)
            return;

        reading.DeletedFromHistoryAt = DateTime.UtcNow;
        await _repo.UpdateAsync(reading, ct);
    }

    private async Task<UserPromptContext> PreparePromptContextAsync(
        CreateReadingRequest request,
        Guid? userId,
        CancellationToken ct)
    {
        if (userId is not { } uid)
            throw new UnauthorizedException("Authentication required");

        return await _personalization.GetPromptContextAsync(
            uid,
            request.ClientDate,
            request.ClientTimeZone,
            ct);
    }

    private async Task<bool> IsHistoryStorageAllowedAsync(
        Guid userId,
        bool requested,
        CancellationToken ct)
    {
        if (!requested) return false;
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new UnauthorizedException("Authentication required");
        return user.HistoryEnabled;
    }

    private async Task<QuestionValidationResult> ValidateQuestionAccessAsync(
        string safeQuestion,
        bool warningAcknowledged,
        CancellationToken ct)
    {
        var validation = await _questionValidator.ValidateAsync(safeQuestion, ct);
        if (validation.Status == QuestionValidationStatus.Accepted)
            return validation;

        var suggestedQuestion = validation.SuggestedQuestion
            ?? QuestionValidationHeuristics.BuildFallbackSuggestion(safeQuestion);

        if (validation.Status == QuestionValidationStatus.Rejected)
            throw new AiPrivacyBlockedException(
                validation.BlockCode ?? "unsafe_question",
                validation.Reason,
                validation.SafeResponse);

        if (!warningAcknowledged)
        {
            throw new QuestionWarningAcknowledgementRequiredException(
                QuestionValidationPolicy.SubscriberWarningMessage,
                validation.Reason,
                QuestionValidationPolicy.ToWireStatus(validation.Status),
                suggestedQuestion);
        }

        return new QuestionValidationResult
        {
            Status = validation.Status,
            Reason = validation.Reason,
            SuggestedQuestion = suggestedQuestion
        };
    }

    private static string BuildQuestionForInterpretation(
        string question,
        QuestionValidationResult validation)
    {
        if (validation.Status == QuestionValidationStatus.Accepted)
            return question;

        var suggestedQuestion = validation.SuggestedQuestion
            ?? QuestionValidationHeuristics.BuildFallbackSuggestion(question);

        var sb = new StringBuilder();
        // Never append the original rejected/rewritten text. Only the locally-created,
        // non-identifying suggestion may cross the external boundary.
        sb.Append("Безопасная формулировка: ").AppendLine(suggestedQuestion);
        sb.AppendLine("Дай только возможный взгляд для личной рефлексии; не давай медицинских, юридических или финансовых указаний и не утверждай будущие события как факты.");
        return sb.ToString();
    }

    private static void EnsureAllowed(AiPrivacyDecision decision)
    {
        if (!decision.CanSendExternally)
            throw new AiPrivacyBlockedException(
                decision.ReasonCode,
                decision.UserMessage,
                decision.SafeResponse);
    }

    private static ReadingResult Map(Reading reading, Spread spread)
        => Map(reading, spread, reading.Question, reading.AiInterpretation);

    private static ReadingResult Map(
        Reading reading,
        Spread spread,
        string question,
        string? interpretation)
    {
        var cards = reading.Cards
            .OrderBy(c => c.Position)
            .Select(c =>
            {
                var position = spread.Positions[c.Position];
                var meaning = c.IsReversed ? c.Card.DescriptionReversed : c.Card.DescriptionUpright;
                return new ReadingCardDto
                {
                    Position = c.Position,
                    PositionName = position.Name,
                    PositionMeaning = position.Meaning,
                    CardId = c.CardId,
                    CardName = c.Card.Name,
                    ImagePath = c.Card.ImagePath,
                    IsReversed = c.IsReversed,
                    Meaning = meaning
                };
            })
            .ToList();

        return new ReadingResult
        {
            Id = reading.Id,
            SpreadType = reading.SpreadType,
            SpreadName = spread.Name,
            Question = question,
            CreatedAt = reading.CreatedAt,
            Cards = cards,
            Interpretation = interpretation,
            DeckType = reading.DeckType
        };
    }
}
