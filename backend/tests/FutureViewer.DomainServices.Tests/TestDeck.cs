using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.Interfaces;

namespace FutureViewer.DomainServices.Tests;

internal sealed class TestDeck : ICardDeck
{
    public Task<IReadOnlyList<TarotCard>> GetAllAsync(CancellationToken ct = default)
    {
        IReadOnlyList<TarotCard> cards = Enumerable.Range(1, 78)
            .Select(i => new TarotCard
            {
                Id = i,
                Name = $"Card {i}",
                Suit = CardSuit.MajorArcana,
                Number = i,
                DescriptionUpright = $"up {i}",
                DescriptionReversed = $"rev {i}",
                ImagePath = $"/cards/{i}.webp"
            })
            .ToList();
        return Task.FromResult(cards);
    }
}
