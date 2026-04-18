using FluentAssertions;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.Services;

namespace FutureViewer.DomainServices.Tests;

public sealed class CardDeckServiceTests
{
    [Fact]
    public async Task Draw_returns_requested_count_of_distinct_cards()
    {
        var sut = new CardDeckService(new TestDeck());

        var drawn = await sut.DrawAsync(10);

        drawn.Should().HaveCount(10);
        drawn.Select(x => x.Card.Id).Distinct().Should().HaveCount(10);
    }

    [Fact]
    public async Task Draw_throws_when_requested_more_than_deck()
    {
        var sut = new CardDeckService(new TestDeck());

        var act = () => sut.DrawAsync(100);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetGlossary_returns_all_cards_with_variants()
    {
        var sut = new CardDeckService(new TestDeck());

        var glossary = await sut.GetGlossaryAsync();

        glossary.Should().HaveCount(78);
        var first = glossary[0];
        first.Id.Should().Be(1);
        first.NameEn.Should().Be("Card 1 EN");
        first.UprightKeywords.Should().ContainSingle().Which.Should().Be("up-kw-1");
        first.DeckVariants.Should().HaveCount(2);
        first.DeckVariants.Select(v => v.DeckType).Should().Contain(new[] { DeckType.RWS, DeckType.Thoth });
    }

    [Fact]
    public async Task GetCardDetail_returns_matching_card_with_variants()
    {
        var sut = new CardDeckService(new TestDeck());

        var detail = await sut.GetCardDetailAsync(42);

        detail.Should().NotBeNull();
        detail!.Id.Should().Be(42);
        detail.ShortUpright.Should().Be("short-up-42");
        detail.DeckVariants.Should().HaveCount(2);
        detail.Aliases.Should().ContainSingle().Which.Should().Be("alias-42");
    }

    [Fact]
    public async Task GetCardDetail_returns_null_for_unknown_id()
    {
        var sut = new CardDeckService(new TestDeck());

        var detail = await sut.GetCardDetailAsync(999);

        detail.Should().BeNull();
    }
}
