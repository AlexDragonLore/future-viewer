using FluentAssertions;
using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;

namespace FutureViewer.Domain.Tests;

public sealed class TarotCardTests
{
    [Fact]
    public void Has_default_glossary_fields_when_created_with_required_only()
    {
        var card = BuildMinimal();

        card.NameEn.Should().BeEmpty();
        card.UprightKeywords.Should().BeEmpty();
        card.ReversedKeywords.Should().BeEmpty();
        card.ShortUpright.Should().BeEmpty();
        card.ShortReversed.Should().BeEmpty();
        card.SuggestedTone.Should().Be(SuggestedTone.Neutral);
        card.Aliases.Should().BeNull();
        card.DeckVariants.Should().BeEmpty();
    }

    [Fact]
    public void Allows_setting_glossary_fields()
    {
        var card = BuildMinimal();
        card.NameEn = "The Fool";
        card.UprightKeywords = new List<string> { "beginnings", "freedom" };
        card.ReversedKeywords = new List<string> { "recklessness" };
        card.ShortUpright = "A fresh start.";
        card.ShortReversed = "A reckless step.";
        card.SuggestedTone = SuggestedTone.Supportive;
        card.Aliases = new List<string> { "Безумец" };

        card.NameEn.Should().Be("The Fool");
        card.UprightKeywords.Should().Contain("freedom");
        card.ReversedKeywords.Should().HaveCount(1);
        card.SuggestedTone.Should().Be(SuggestedTone.Supportive);
        card.Aliases.Should().Contain("Безумец");
    }

    private static TarotCard BuildMinimal() => new()
    {
        Id = 1,
        Name = "Шут",
        Suit = CardSuit.MajorArcana,
        Number = 0,
        DescriptionUpright = "up",
        DescriptionReversed = "rev",
        ImagePath = "/img/fool.png"
    };
}

public sealed class DeckVariantTests
{
    [Fact]
    public void Is_created_with_required_fields()
    {
        var variant = new DeckVariant
        {
            CardId = 1,
            DeckType = DeckType.Thoth,
            VariantNote = "В Thoth акцент на эзотерике."
        };

        variant.CardId.Should().Be(1);
        variant.DeckType.Should().Be(DeckType.Thoth);
        variant.VariantNote.Should().Contain("Thoth");
        variant.Card.Should().BeNull();
    }
}
