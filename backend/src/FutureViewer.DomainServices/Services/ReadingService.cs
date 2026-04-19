using System.Runtime.CompilerServices;
using System.Text;
using FutureViewer.Domain.Entities;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Exceptions;
using FutureViewer.DomainServices.Interfaces;

namespace FutureViewer.DomainServices.Services;

public sealed class ReadingService
{
    private readonly IReadingRepository _repo;
    private readonly CardDeckService _deck;
    private readonly InterpretationService _interpreter;
    private readonly SubscriptionService _subscription;
    private readonly FeedbackService _feedback;

    public ReadingService(
        IReadingRepository repo,
        CardDeckService deck,
        InterpretationService interpreter,
        SubscriptionService subscription,
        FeedbackService feedback)
    {
        _repo = repo;
        _deck = deck;
        _interpreter = interpreter;
        _subscription = subscription;
        _feedback = feedback;
    }

    public async Task<ReadingResult> CreateAsync(
        CreateReadingRequest request,
        Guid? userId,
        CancellationToken ct = default)
    {
        var spread = Spread.From(request.SpreadType);
        if (userId is { } uid)
            await _subscription.EnsureReadingAllowedAsync(uid, spread.Type, ct);
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
            spread, request.Question, cards, request.DeckType, variantNotes, ct);

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
            try { await _feedback.ScheduleAsync(reading, ct); }
            catch { /* best-effort — don't fail the reading creation */ }
        }

        return Map(reading, spread);
    }

    public async IAsyncEnumerable<ReadingStreamEvent> CreateStreamAsync(
        CreateReadingRequest request,
        Guid? userId,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var spread = Spread.From(request.SpreadType);
        if (userId is { } uid)
            await _subscription.EnsureReadingAllowedAsync(uid, spread.Type, ct);
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
                spread, request.Question, cards, request.DeckType, variantNotes, ct))
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
                try { await _feedback.ScheduleAsync(reading, CancellationToken.None); }
                catch { /* best-effort */ }
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
