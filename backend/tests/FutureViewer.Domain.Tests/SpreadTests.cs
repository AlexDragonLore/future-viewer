using FluentAssertions;
using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;

namespace FutureViewer.Domain.Tests;

public sealed class SpreadTests
{
    [Fact]
    public void SingleCard_has_one_position()
    {
        Spread.SingleCard.CardCount.Should().Be(1);
    }

    [Fact]
    public void ThreeCard_has_three_positions()
    {
        Spread.ThreeCard.CardCount.Should().Be(3);
        Spread.ThreeCard.Positions.Select(p => p.Name)
            .Should().Equal("Прошлое", "Настоящее", "Будущее");
    }

    [Fact]
    public void CelticCross_has_ten_positions()
    {
        Spread.CelticCross.CardCount.Should().Be(10);
    }

    [Theory]
    [InlineData(SpreadType.SingleCard, 1)]
    [InlineData(SpreadType.ThreeCard, 3)]
    [InlineData(SpreadType.CelticCross, 10)]
    public void From_returns_matching_spread(SpreadType type, int expectedCount)
    {
        var spread = Spread.From(type);
        spread.Type.Should().Be(type);
        spread.CardCount.Should().Be(expectedCount);
    }

    [Fact]
    public void From_throws_for_unknown_type()
    {
        var act = () => Spread.From((SpreadType)999);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
