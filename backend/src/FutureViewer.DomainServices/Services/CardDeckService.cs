using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Interfaces;

namespace FutureViewer.DomainServices.Services;

public sealed class CardDeckService
{
    private readonly ICardDeck _deck;

    public CardDeckService(ICardDeck deck)
    {
        _deck = deck;
    }

    public async Task<IReadOnlyList<(TarotCard Card, bool IsReversed)>> DrawAsync(
        int count,
        CancellationToken ct = default)
    {
        var all = await _deck.GetAllAsync(ct);
        if (all.Count < count)
            throw new InvalidOperationException($"Deck has only {all.Count} cards, requested {count}");

        var shuffled = all.ToArray();
        Shuffle(shuffled);

        var drawn = new List<(TarotCard, bool)>(count);
        for (var i = 0; i < count; i++)
        {
            var reversed = Random.Shared.NextDouble() < 0.5;
            drawn.Add((shuffled[i], reversed));
        }
        return drawn;
    }

    public async Task<IReadOnlyList<CardGlossaryDto>> GetGlossaryAsync(CancellationToken ct = default)
    {
        var cards = await _deck.GetAllWithVariantsAsync(ct);
        return cards.Select(ToDto).ToList();
    }

    public async Task<CardGlossaryDto?> GetCardDetailAsync(int id, CancellationToken ct = default)
    {
        var card = await _deck.GetByIdWithVariantsAsync(id, ct);
        return card is null ? null : ToDto(card);
    }

    public Task<IReadOnlyDictionary<int, string>> GetVariantNotesAsync(
        DeckType deckType,
        IReadOnlyCollection<int> cardIds,
        CancellationToken ct = default)
    {
        return _deck.GetVariantNotesAsync(deckType, cardIds, ct);
    }

    private static CardGlossaryDto ToDto(TarotCard card) => new()
    {
        Id = card.Id,
        Name = card.Name,
        NameEn = card.NameEn,
        Suit = card.Suit,
        Number = card.Number,
        ImagePath = card.ImagePath,
        DescriptionUpright = card.DescriptionUpright,
        DescriptionReversed = card.DescriptionReversed,
        ShortUpright = card.ShortUpright,
        ShortReversed = card.ShortReversed,
        UprightKeywords = card.UprightKeywords.ToList(),
        ReversedKeywords = card.ReversedKeywords.ToList(),
        SuggestedTone = card.SuggestedTone,
        Aliases = card.Aliases?.ToList() ?? new List<string>(),
        DeckVariants = card.DeckVariants
            .Select(v => new DeckVariantDto { DeckType = v.DeckType, VariantNote = v.VariantNote })
            .ToList()
    };

    private static void Shuffle<T>(T[] array)
    {
        for (var i = array.Length - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (array[i], array[j]) = (array[j], array[i]);
        }
    }
}
