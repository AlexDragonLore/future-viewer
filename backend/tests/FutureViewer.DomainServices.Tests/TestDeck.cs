using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.Interfaces;

namespace FutureViewer.DomainServices.Tests;

internal sealed class TestDeck : ICardDeck
{
    private static List<TarotCard> BuildCards() => Enumerable.Range(1, 78)
        .Select(i => new TarotCard
        {
            Id = i,
            Name = $"Card {i}",
            Suit = CardSuit.MajorArcana,
            Number = i,
            DescriptionUpright = $"up {i}",
            DescriptionReversed = $"rev {i}",
            ImagePath = $"/cards/{i}.webp",
            NameEn = $"Card {i} EN",
            UprightKeywords = new List<string> { $"up-kw-{i}" },
            ReversedKeywords = new List<string> { $"rev-kw-{i}" },
            ShortUpright = $"short-up-{i}",
            ShortReversed = $"short-rev-{i}",
            SuggestedTone = SuggestedTone.Neutral,
            Aliases = new List<string> { $"alias-{i}" },
            DeckVariants = new List<DeckVariant>
            {
                new() { CardId = i, DeckType = DeckType.RWS, VariantNote = $"RWS note {i}" },
                new() { CardId = i, DeckType = DeckType.Thoth, VariantNote = $"Thoth note {i}" }
            }
        })
        .ToList();

    public Task<IReadOnlyList<TarotCard>> GetAllAsync(CancellationToken ct = default)
    {
        IReadOnlyList<TarotCard> cards = BuildCards();
        return Task.FromResult(cards);
    }

    public Task<IReadOnlyList<TarotCard>> GetAllWithVariantsAsync(CancellationToken ct = default)
    {
        IReadOnlyList<TarotCard> cards = BuildCards();
        return Task.FromResult(cards);
    }

    public Task<TarotCard?> GetByIdWithVariantsAsync(int id, CancellationToken ct = default)
    {
        var card = BuildCards().FirstOrDefault(c => c.Id == id);
        return Task.FromResult(card);
    }

    public Task<IReadOnlyDictionary<int, string>> GetVariantNotesAsync(
        DeckType deckType,
        IReadOnlyCollection<int> cardIds,
        CancellationToken ct = default)
    {
        var all = BuildCards();
        var dict = all
            .Where(c => cardIds.Contains(c.Id))
            .SelectMany(c => c.DeckVariants)
            .Where(v => v.DeckType == deckType)
            .ToDictionary(v => v.CardId, v => v.VariantNote);
        IReadOnlyDictionary<int, string> result = dict;
        return Task.FromResult(result);
    }
}
