using FluentAssertions;
using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.Domain.ValueObjects;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Interfaces;
using FutureViewer.DomainServices.Services;
using Moq;

namespace FutureViewer.DomainServices.Tests;

public sealed class ReadingServiceTests
{
    [Fact]
    public async Task CreateAsync_draws_cards_saves_and_sets_interpretation()
    {
        var repo = new Mock<IReadingRepository>();
        repo.Setup(r => r.AddAsync(It.IsAny<Reading>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Reading r, CancellationToken _) => r);

        var deck = new CardDeckService(new TestDeck());

        var ai = new Mock<IAIInterpreter>();
        ai.Setup(a => a.InterpretAsync(
                It.IsAny<Spread>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<ReadingCard>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InterpretationResult
            {
                Text = "mystical text",
                Model = "stub-model",
                GeneratedAt = DateTime.UtcNow
            });
        var interpret = new InterpretationService(ai.Object);

        var sut = new ReadingService(repo.Object, deck, interpret);

        var result = await sut.CreateAsync(
            new CreateReadingRequest { SpreadType = SpreadType.ThreeCard, Question = "Что меня ждёт?" },
            userId: Guid.NewGuid());

        result.Cards.Should().HaveCount(3);
        result.Interpretation.Should().Be("mystical text");
        result.SpreadName.Should().Be("Прошлое — Настоящее — Будущее");
        repo.Verify(r => r.AddAsync(It.IsAny<Reading>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
