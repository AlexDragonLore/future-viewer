using FluentAssertions;
using FutureViewer.Domain.Enums;
using FutureViewer.Infrastructure.Persistence;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class TarotDeckSeedTests
{
    [Fact]
    public void BuildDeck_Returns78UniqueCardsWithPopulatedGlossaryFields()
    {
        var deck = TarotDeckSeed.BuildDeck();

        deck.Should().HaveCount(78);
        deck.Select(c => c.Id).Should().OnlyHaveUniqueItems();
        deck.Should().OnlyContain(c => !string.IsNullOrWhiteSpace(c.NameEn));
        deck.Should().OnlyContain(c => !string.IsNullOrWhiteSpace(c.ShortUpright));
        deck.Should().OnlyContain(c => !string.IsNullOrWhiteSpace(c.ShortReversed));
        deck.Should().OnlyContain(c => c.UprightKeywords.Count > 0);
        deck.Should().OnlyContain(c => c.ReversedKeywords.Count > 0);

        deck.Count(c => c.Suit == CardSuit.MajorArcana).Should().Be(22);
        deck.Count(c => c.Suit == CardSuit.Wands).Should().Be(14);
        deck.Count(c => c.Suit == CardSuit.Cups).Should().Be(14);
        deck.Count(c => c.Suit == CardSuit.Swords).Should().Be(14);
        deck.Count(c => c.Suit == CardSuit.Pentacles).Should().Be(14);
    }

    [Fact]
    public void BuildDeckVariants_Returns390RecordsCoveringAllDeckTypes()
    {
        var deck = TarotDeckSeed.BuildDeck();
        var variants = TarotDeckSeed.BuildDeckVariants(deck);

        variants.Should().HaveCount(78 * 5);
        variants.Select(v => (v.CardId, v.DeckType)).Should().OnlyHaveUniqueItems();

        foreach (var deckType in Enum.GetValues<DeckType>())
        {
            variants.Count(v => v.DeckType == deckType).Should().Be(78);
        }

        variants.Should().OnlyContain(v => !string.IsNullOrWhiteSpace(v.VariantNote));
    }
}
