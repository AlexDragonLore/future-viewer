using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Exceptions;
using FutureViewer.DomainServices.Interfaces;

namespace FutureViewer.DomainServices.Services;

public sealed class ReadingService
{
    private readonly IReadingRepository _repo;
    private readonly CardDeckService _deck;
    private readonly InterpretationService _interpreter;

    public ReadingService(
        IReadingRepository repo,
        CardDeckService deck,
        InterpretationService interpreter)
    {
        _repo = repo;
        _deck = deck;
        _interpreter = interpreter;
    }

    public async Task<ReadingResult> CreateAsync(
        CreateReadingRequest request,
        Guid? userId,
        CancellationToken ct = default)
    {
        var spread = Spread.From(request.SpreadType);
        var drawn = await _deck.DrawAsync(spread.CardCount, ct);

        var reading = new Reading
        {
            UserId = userId,
            SpreadType = spread.Type,
            Question = request.Question,
            CreatedAt = DateTime.UtcNow,
            Cards = drawn
                .Select((x, idx) => new ReadingCard
                {
                    CardId = x.Card.Id,
                    Card = x.Card,
                    Position = idx,
                    IsReversed = x.IsReversed
                })
                .ToList()
        };

        await _repo.AddAsync(reading, ct);

        var interpretation = await _interpreter.InterpretAsync(reading, spread, ct);
        reading.AiInterpretation = interpretation.Text;
        reading.AiModel = interpretation.Model;
        await _repo.UpdateAsync(reading, ct);

        return Map(reading, spread);
    }

    public async Task<ReadingResult> GetAsync(Guid id, CancellationToken ct = default)
    {
        var reading = await _repo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Reading {id} not found");
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
                return new ReadingCardDto(
                    c.Position,
                    position.Name,
                    position.Meaning,
                    c.CardId,
                    c.Card.Name,
                    c.Card.ImagePath,
                    c.IsReversed,
                    meaning);
            })
            .ToList();

        return new ReadingResult(
            reading.Id,
            reading.SpreadType,
            spread.Name,
            reading.Question,
            reading.CreatedAt,
            cards,
            reading.AiInterpretation);
    }
}
