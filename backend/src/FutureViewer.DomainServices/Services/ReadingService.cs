using System.Runtime.CompilerServices;
using System.Text;
using FutureViewer.Domain.Entities;
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
    private readonly IAIMemoryExtractor _memoryExtractor;
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
        ILogger<ReadingService> logger)
    {
        _repo = repo;
        _deck = deck;
        _interpreter = interpreter;
        _subscription = subscription;
        _feedback = feedback;
        _personalization = personalization;
        _questionValidator = questionValidator;
        _memoryExtractor = memoryExtractor;
        _logger = logger;
    }

    public async Task<ReadingResult> CreateAsync(
        CreateReadingRequest request,
        Guid? userId,
        CancellationToken ct = default)
    {
        var spread = Spread.From(request.SpreadType);
        var promptContext = await PreparePromptContextAsync(request, userId, ct);
        if (userId is { } uid)
            await _subscription.EnsureReadingAllowedAsync(uid, spread.Type, ct);
        await ValidateQuestionAsync(request.Question, ct);
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

        var interpretation = await _interpreter.InterpretAsync(
            spread, request.Question, cards, request.DeckType, variantNotes, promptContext, ct);

        var reading = new Reading
        {
            UserId = userId,
            SpreadType = spread.Type,
            Question = request.Question,
            AiInterpretation = interpretation.Text,
            AiModel = interpretation.Model,
            DeckType = request.DeckType,
            Cards = cards
        };

        await _repo.AddAsync(reading, ct);

        if (reading.UserId is not null)
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
                _logger.LogWarning(ex, "Failed to schedule feedback for reading {ReadingId}", reading.Id);
            }

            await RememberAsync(reading.UserId.Value, request.Question, interpretation.Text, promptContext, ct);
        }

        return Map(reading, spread);
    }

    public async IAsyncEnumerable<ReadingStreamEvent> CreateStreamAsync(
        CreateReadingRequest request,
        Guid? userId,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var spread = Spread.From(request.SpreadType);
        var promptContext = await PreparePromptContextAsync(request, userId, ct);
        if (userId is { } uid)
            await _subscription.EnsureReadingAllowedAsync(uid, spread.Type, ct);
        await ValidateQuestionAsync(request.Question, ct);
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
            Question = request.Question,
            AiInterpretation = null,
            AiModel = _interpreter.Model,
            DeckType = request.DeckType,
            Cards = cards
        };

        yield return new ReadingStreamEvent.Cards(Map(reading, spread));

        var sb = new StringBuilder();
        var persisted = false;
        try
        {
            await foreach (var delta in _interpreter.InterpretStreamAsync(
                spread, request.Question, cards, request.DeckType, variantNotes, promptContext, ct))
            {
                if (!persisted)
                {
                    await _repo.AddAsync(reading, ct);
                    persisted = true;
                }
                sb.Append(delta);
                yield return new ReadingStreamEvent.Chunk(delta);
            }
        }
        finally
        {
            if (persisted && sb.Length > 0)
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

            if (persisted && reading.UserId is not null)
            {
                try
                {
                    await _feedback.ScheduleAsync(reading, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to schedule feedback for streaming reading {ReadingId}", reading.Id);
                }

                if (sb.Length > 0)
                    await RememberAsync(reading.UserId.Value, request.Question, sb.ToString(), promptContext, CancellationToken.None);
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
        var spread = Spread.From(reading.SpreadType);
        return Map(reading, spread);
    }

    public async Task<IReadOnlyList<ReadingResult>> GetHistoryAsync(Guid userId, CancellationToken ct = default)
    {
        var readings = await _repo.GetHistoryAsync(userId, take: 50, ct);
        return readings.Select(r => Map(r, Spread.From(r.SpreadType))).ToList();
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

    private async Task ValidateQuestionAsync(string question, CancellationToken ct)
    {
        var validation = await _questionValidator.ValidateAsync(question, ct);
        if (validation.Status == QuestionValidationStatus.Accepted) return;

        var code = validation.Status == QuestionValidationStatus.NeedsRewrite
            ? "question_needs_rewrite"
            : "question_rejected";

        throw new QuestionValidationException(code, validation.Reason, validation.SuggestedQuestion);
    }

    private async Task RememberAsync(
        Guid userId,
        string question,
        string interpretation,
        UserPromptContext promptContext,
        CancellationToken ct)
    {
        try
        {
            var rules = await _memoryExtractor.ExtractAsync(new MemoryExtractionContext
            {
                Question = question,
                Interpretation = interpretation,
                PromptContext = promptContext
            }, ct);
            await _personalization.SaveExtractedMemoryAsync(userId, rules, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract memory for user {UserId}", userId);
        }
    }

    private static ReadingResult Map(Reading reading, Spread spread)
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
            Question = reading.Question,
            CreatedAt = reading.CreatedAt,
            Cards = cards,
            Interpretation = reading.AiInterpretation,
            DeckType = reading.DeckType
        };
    }
}
