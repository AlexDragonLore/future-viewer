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
    private sealed class PassThroughUnitOfWork : IUnitOfWork
    {
        public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default) =>
            work(ct);
    }

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
                BirthYear = 1815,
                SubscriptionStatus = SubscriptionStatus.Active,
                SubscriptionExpiresAt = DateTime.UtcNow.AddDays(5)
            });
        var memory = new Mock<IUserMemoryRepository>();
        memory.Setup(m => m.GetByUserAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserMemoryRule>());
        var subscription = new SubscriptionService(users.Object, repo.Object, Mock.Of<IPaymentProvider>(), Mock.Of<IProcessedPaymentRepository>(), new PassThroughUnitOfWork());
        var feedback = new FeedbackService(Mock.Of<IFeedbackRepository>(), repo.Object, Mock.Of<IFeedbackScorer>());
        var personalization = new PersonalizationService(users.Object, memory.Object);
        var questionValidator = AcceptedQuestionValidator();
        var memoryExtractor = EmptyMemoryExtractor();

        var sut = new ReadingService(repo.Object, deck, interpret, subscription, feedback, personalization, questionValidator.Object, memoryExtractor.Object, users.Object, NullLogger<ReadingService>.Instance);

        var result = await sut.CreateAsync(
            new CreateReadingRequest { SpreadType = SpreadType.ThreeCard, Question = "Что меня ждёт?" },
            userId: Guid.NewGuid());

        result.Cards.Should().HaveCount(3);
        result.Interpretation.Should().Be("mystical text");
        result.SpreadName.Should().Be("Прошлое — Настоящее — Будущее");
        result.DeckType.Should().Be(DeckType.RWS);
        repo.Verify(r => r.AddAsync(It.IsAny<Reading>(), It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.UpdateAsync(It.Is<Reading>(reading => reading.SavedToHistory
            && reading.Question == "Что меня ждёт?"
            && reading.AiInterpretation == "mystical text"), It.IsAny<CancellationToken>()), Times.Once);
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
                HistoryEnabled = false,
                FirstName = "Ada",
                LastName = "Lovelace",
                BirthYear = 1815,
                SubscriptionStatus = SubscriptionStatus.Active,
                SubscriptionExpiresAt = DateTime.UtcNow.AddDays(5)
            });
        var memory = new Mock<IUserMemoryRepository>();
        memory.Setup(m => m.GetByUserAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserMemoryRule>());
        var subscription = new SubscriptionService(users.Object, repo.Object, Mock.Of<IPaymentProvider>(), Mock.Of<IProcessedPaymentRepository>(), new PassThroughUnitOfWork());
        var feedback = new FeedbackService(Mock.Of<IFeedbackRepository>(), repo.Object, Mock.Of<IFeedbackScorer>());
        var personalization = new PersonalizationService(users.Object, memory.Object);
        var questionValidator = AcceptedQuestionValidator();
        var memoryExtractor = EmptyMemoryExtractor();

        var sut = new ReadingService(repo.Object, deck, interpret, subscription, feedback, personalization, questionValidator.Object, memoryExtractor.Object, users.Object, NullLogger<ReadingService>.Instance);

        var result = await sut.CreateAsync(
            new CreateReadingRequest
            {
                SpreadType = SpreadType.SingleCard,
                Question = "q",
                DeckType = DeckType.Thoth,
                SaveToHistory = false
            },
            userId: Guid.NewGuid());

        capturedDeck.Should().Be(DeckType.Thoth);
        capturedNotes.Should().NotBeNull();
        capturedNotes!.Values.Should().OnlyContain(v => v.Contains("Thoth"));
        saved!.DeckType.Should().Be(DeckType.Thoth);
        saved.SavedToHistory.Should().BeTrue("authenticated readings ignore the legacy account and request switches");
        saved.Question.Should().Be("q");
        saved.AiInterpretation.Should().Be("ok");
        result.Question.Should().Be("q");
        result.DeckType.Should().Be(DeckType.Thoth);
    }

    [Fact]
    public async Task CreateAsync_requires_warning_acknowledgement_when_question_needs_rewrite()
    {
        var repo = new Mock<IReadingRepository>();
        var ai = new Mock<IAIInterpreter>();
        var interpret = new InterpretationService(ai.Object);
        var users = CompleteUserRepo();
        var memory = EmptyMemoryRepo();
        var subscription = new SubscriptionService(users.Object, repo.Object, Mock.Of<IPaymentProvider>(), Mock.Of<IProcessedPaymentRepository>(), new PassThroughUnitOfWork());
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
            users.Object,
            NullLogger<ReadingService>.Instance);

        var act = () => sut.CreateAsync(
            new CreateReadingRequest { SpreadType = SpreadType.SingleCard, Question = "что будет?" },
            Guid.NewGuid());

        await act.Should().ThrowAsync<QuestionWarningAcknowledgementRequiredException>();
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

    [Fact]
    public async Task CreateAsync_continues_when_subscriber_acknowledges_question_warning()
    {
        const string safeSuggestion = "На что мне обратить внимание?";
        var repo = new Mock<IReadingRepository>();
        repo.Setup(r => r.AddAsync(It.IsAny<Reading>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Reading r, CancellationToken _) => r);
        var ai = new Mock<IAIInterpreter>();
        ai.Setup(a => a.InterpretAsync(
                It.IsAny<Spread>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<ReadingCard>>(),
                It.IsAny<DeckType>(),
                It.IsAny<IReadOnlyDictionary<int, string>>(),
                It.IsAny<UserPromptContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InterpretationResult { Text = "ok", Model = "stub", GeneratedAt = DateTime.UtcNow });
        var users = CompleteUserRepo();
        var subscription = new SubscriptionService(users.Object, repo.Object, Mock.Of<IPaymentProvider>(), Mock.Of<IProcessedPaymentRepository>(), new PassThroughUnitOfWork());
        var feedback = new FeedbackService(Mock.Of<IFeedbackRepository>(), repo.Object, Mock.Of<IFeedbackScorer>());
        var personalization = new PersonalizationService(users.Object, EmptyMemoryRepo().Object);
        var questionValidator = new Mock<IAIQuestionValidator>();
        questionValidator.Setup(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QuestionValidationResult
            {
                Status = QuestionValidationStatus.NeedsRewrite,
                Reason = "Лучше уточнить.",
                SuggestedQuestion = safeSuggestion
            });

        var sut = new ReadingService(
            repo.Object,
            new CardDeckService(new TestDeck()),
            new InterpretationService(ai.Object),
            subscription,
            feedback,
            personalization,
            questionValidator.Object,
            EmptyMemoryExtractor().Object,
            users.Object,
            NullLogger<ReadingService>.Instance);

        var result = await sut.CreateAsync(
            new CreateReadingRequest
            {
                SpreadType = SpreadType.SingleCard,
                Question = "что будет?",
                QuestionWarningAcknowledged = true
            },
            Guid.NewGuid());

        result.Interpretation.Should().Be("ok");
        repo.Verify(r => r.AddAsync(It.IsAny<Reading>(), It.IsAny<CancellationToken>()), Times.Once);
        ai.Verify(a => a.InterpretAsync(
            It.IsAny<Spread>(),
            safeSuggestion,
            It.IsAny<IReadOnlyList<ReadingCard>>(),
            It.IsAny<DeckType>(),
            It.IsAny<IReadOnlyDictionary<int, string>>(),
            It.IsAny<UserPromptContext>(),
            It.IsAny<CancellationToken>()), Times.Once);
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
                BirthYear = 1815,
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
