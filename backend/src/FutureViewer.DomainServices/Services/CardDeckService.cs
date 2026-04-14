using FutureViewer.Domain.Entities;
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

    private static void Shuffle<T>(T[] array)
    {
        for (var i = array.Length - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (array[i], array[j]) = (array[j], array[i]);
        }
    }
}
