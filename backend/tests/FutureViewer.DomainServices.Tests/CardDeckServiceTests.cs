using FluentAssertions;
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
}
