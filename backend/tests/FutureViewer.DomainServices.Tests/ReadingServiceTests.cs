using FluentAssertions;
using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.Domain.ValueObjects;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Exceptions;
using FutureViewer.DomainServices.Interfaces;
using FutureViewer.DomainServices.Services;
using Microsoft.Extensions.Logging.Abstractions;
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
                It.IsAny<UserPromptContext>(),
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
                FirstName = "Ada",
                LastName = "Lovelace",
                BirthDate = new DateOnly(1815, 12, 10),
                SubscriptionStatus = SubscriptionStatus.Active,
                SubscriptionExpiresAt = DateTime.UtcNow.AddDays(5)
            });
        var memory = new Mock<IUserMemoryRepository>();
        memory.Setup(m => m.GetByUserAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserMemoryRule>());
        var subscription = new SubscriptionService(users.Object, repo.Object, Mock.Of<IPaymentProvider>(), Mock.Of<IProcessedPaymentRepository>(), Mock.Of<IUnitOfWork>());
        var feedback = new FeedbackService(Mock.Of<IFeedbackRepository>(), repo.Object, Mock.Of<IFeedbackScorer>());
        var personalization = new PersonalizationService(users.Object, memory.Object);
        var questionValidator = AcceptedQuestionValidator();
        var memoryExtractor = EmptyMemoryExtractor();

        var sut = new ReadingService(repo.Object, deck, interpret, subscription, feedback, personalization, questionValidator.Object, memoryExtractor.Object, NullLogger<ReadingService>.Instance);

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
                It.IsAny<UserPromptContext>(),
                It.IsAny<CancellationToken>()))
            .Callback<Spread, string, IReadOnlyList<ReadingCard>, DeckType, IReadOnlyDictionary<int, string>, UserPromptContext, CancellationToken>(
                (_, _, _, d, n, _, _) => { capturedDeck = d; capturedNotes = n; })
            .ReturnsAsync(new InterpretationResult { Text = "ok", Model = "stub", GeneratedAt = DateTime.UtcNow });
        var interpret = new InterpretationService(ai.Object);

        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => new User
            {
                Id = id,
                Email = "a@b.c",
                PasswordHash = "x",
                FirstName = "Ada",
                LastName = "Lovelace",
                BirthDate = new DateOnly(1815, 12, 10),
                SubscriptionStatus = SubscriptionStatus.Active,
                SubscriptionExpiresAt = DateTime.UtcNow.AddDays(5)
            });
        var memory = new Mock<IUserMemoryRepository>();
        memory.Setup(m => m.GetByUserAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserMemoryRule>());
        var subscription = new SubscriptionService(users.Object, repo.Object, Mock.Of<IPaymentProvider>(), Mock.Of<IProcessedPaymentRepository>(), Mock.Of<IUnitOfWork>());
        var feedback = new FeedbackService(Mock.Of<IFeedbackRepository>(), repo.Object, Mock.Of<IFeedbackScorer>());
        var personalization = new PersonalizationService(users.Object, memory.Object);
        var questionValidator = AcceptedQuestionValidator();
        var memoryExtractor = EmptyMemoryExtractor();

        var sut = new ReadingService(repo.Object, deck, interpret, subscription, feedback, personalization, questionValidator.Object, memoryExtractor.Object, NullLogger<ReadingService>.Instance);

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

    [Fact]
    public async Task CreateAsync_stops_before_drawing_cards_when_question_needs_rewrite()
    {
        var repo = new Mock<IReadingRepository>();
        var ai = new Mock<IAIInterpreter>();
        var interpret = new InterpretationService(ai.Object);
        var users = CompleteUserRepo();
        var memory = EmptyMemoryRepo();
        var subscription = new SubscriptionService(users.Object, repo.Object, Mock.Of<IPaymentProvider>(), Mock.Of<IProcessedPaymentRepository>(), Mock.Of<IUnitOfWork>());
        var feedback = new FeedbackService(Mock.Of<IFeedbackRepository>(), repo.Object, Mock.Of<IFeedbackScorer>());
        var personalization = new PersonalizationService(users.Object, memory.Object);
        var questionValidator = new Mock<IAIQuestionValidator>();
        questionValidator.Setup(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QuestionValidationResult
            {
                Status = QuestionValidationStatus.NeedsRewrite,
                Reason = "Лучше уточнить.",
                SuggestedQuestion = "На что мне обратить внимание?"
            });

        var sut = new ReadingService(
            repo.Object,
            new CardDeckService(new TestDeck()),
            interpret,
            subscription,
            feedback,
            personalization,
            questionValidator.Object,
            EmptyMemoryExtractor().Object,
            NullLogger<ReadingService>.Instance);

        var act = () => sut.CreateAsync(
            new CreateReadingRequest { SpreadType = SpreadType.SingleCard, Question = "что будет?" },
            Guid.NewGuid());

        await act.Should().ThrowAsync<QuestionValidationException>()
            .Where(ex => ex.ErrorCode == "question_needs_rewrite");
        repo.Verify(r => r.AddAsync(It.IsAny<Reading>(), It.IsAny<CancellationToken>()), Times.Never);
        ai.Verify(a => a.InterpretAsync(
            It.IsAny<Spread>(),
            It.IsAny<string>(),
            It.IsAny<IReadOnlyList<ReadingCard>>(),
            It.IsAny<DeckType>(),
            It.IsAny<IReadOnlyDictionary<int, string>>(),
            It.IsAny<UserPromptContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<IUserRepository> CompleteUserRepo()
    {
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => new User
            {
                Id = id,
                Email = "a@b.c",
                PasswordHash = "x",
                FirstName = "Ada",
                LastName = "Lovelace",
                BirthDate = new DateOnly(1815, 12, 10),
                SubscriptionStatus = SubscriptionStatus.Active,
                SubscriptionExpiresAt = DateTime.UtcNow.AddDays(5)
            });
        return users;
    }

    private static Mock<IUserMemoryRepository> EmptyMemoryRepo()
    {
        var memory = new Mock<IUserMemoryRepository>();
        memory.Setup(m => m.GetByUserAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserMemoryRule>());
        return memory;
    }

    private static Mock<IAIQuestionValidator> AcceptedQuestionValidator()
    {
        var validator = new Mock<IAIQuestionValidator>();
        validator.Setup(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QuestionValidationResult
            {
                Status = QuestionValidationStatus.Accepted,
                Reason = "ok",
                SuggestedQuestion = null
            });
        return validator;
    }

    private static Mock<IAIMemoryExtractor> EmptyMemoryExtractor()
    {
        var extractor = new Mock<IAIMemoryExtractor>();
        extractor.Setup(e => e.ExtractAsync(It.IsAny<MemoryExtractionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());
        return extractor;
    }
}
