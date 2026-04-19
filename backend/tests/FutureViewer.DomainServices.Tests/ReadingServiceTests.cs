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
                It.IsAny<DeckType>(),
                It.IsAny<IReadOnlyDictionary<int, string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InterpretationResult
            {
                Text = "mystical text",
                Model = "stub-model",
                GeneratedAt = DateTime.UtcNow
            });
        var interpret = new InterpretationService(ai.Object);

        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => new User
            {
                Id = id,
                Email = "a@b.c",
                PasswordHash = "x",
                SubscriptionStatus = SubscriptionStatus.Active,
                SubscriptionExpiresAt = DateTime.UtcNow.AddDays(5)
            });
        var subscription = new SubscriptionService(users.Object, repo.Object, Mock.Of<IPaymentProvider>(), Mock.Of<IProcessedPaymentRepository>(), Mock.Of<IUnitOfWork>());

        var sut = new ReadingService(repo.Object, deck, interpret, subscription);

        var result = await sut.CreateAsync(
            new CreateReadingRequest { SpreadType = SpreadType.ThreeCard, Question = "Что меня ждёт?" },
            userId: Guid.NewGuid());

        result.Cards.Should().HaveCount(3);
        result.Interpretation.Should().Be("mystical text");
        result.SpreadName.Should().Be("Прошлое — Настоящее — Будущее");
        result.DeckType.Should().Be(DeckType.RWS);
        repo.Verify(r => r.AddAsync(It.IsAny<Reading>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_passes_deck_type_and_variant_notes_to_interpreter_and_reading()
    {
        var repo = new Mock<IReadingRepository>();
        Reading? saved = null;
        repo.Setup(r => r.AddAsync(It.IsAny<Reading>(), It.IsAny<CancellationToken>()))
            .Callback((Reading r, CancellationToken _) => saved = r)
            .ReturnsAsync((Reading r, CancellationToken _) => r);

        var deck = new CardDeckService(new TestDeck());

        DeckType? capturedDeck = null;
        IReadOnlyDictionary<int, string>? capturedNotes = null;
        var ai = new Mock<IAIInterpreter>();
        ai.Setup(a => a.InterpretAsync(
                It.IsAny<Spread>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<ReadingCard>>(),
                It.IsAny<DeckType>(),
                It.IsAny<IReadOnlyDictionary<int, string>>(),
                It.IsAny<CancellationToken>()))
            .Callback<Spread, string, IReadOnlyList<ReadingCard>, DeckType, IReadOnlyDictionary<int, string>, CancellationToken>(
                (_, _, _, d, n, _) => { capturedDeck = d; capturedNotes = n; })
            .ReturnsAsync(new InterpretationResult { Text = "ok", Model = "stub", GeneratedAt = DateTime.UtcNow });
        var interpret = new InterpretationService(ai.Object);

        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => new User
            {
                Id = id,
                Email = "a@b.c",
                PasswordHash = "x",
                SubscriptionStatus = SubscriptionStatus.Active,
                SubscriptionExpiresAt = DateTime.UtcNow.AddDays(5)
            });
        var subscription = new SubscriptionService(users.Object, repo.Object, Mock.Of<IPaymentProvider>(), Mock.Of<IProcessedPaymentRepository>(), Mock.Of<IUnitOfWork>());

        var sut = new ReadingService(repo.Object, deck, interpret, subscription);

        var result = await sut.CreateAsync(
            new CreateReadingRequest
            {
                SpreadType = SpreadType.SingleCard,
                Question = "q",
                DeckType = DeckType.Thoth
            },
            userId: Guid.NewGuid());

        capturedDeck.Should().Be(DeckType.Thoth);
        capturedNotes.Should().NotBeNull();
        capturedNotes!.Values.Should().OnlyContain(v => v.Contains("Thoth"));
        saved!.DeckType.Should().Be(DeckType.Thoth);
        result.DeckType.Should().Be(DeckType.Thoth);
    }
}
