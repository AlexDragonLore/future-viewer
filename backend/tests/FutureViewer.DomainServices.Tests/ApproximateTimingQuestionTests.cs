using FluentAssertions;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Services;

namespace FutureViewer.DomainServices.Tests;

public sealed class ApproximateTimingQuestionTests
{
    [Theory]
    [InlineData("Назови точную дату переезда", "примерную дату переезда")]
    [InlineData("Какая точная дата переезда?", "примерная дата переезда")]
    [InlineData("Можно узнать о точной дате переезда?", "примерной дате переезда")]
    [InlineData("Можно узнать точные даты поездки?", "примерные даты поездки")]
    [InlineData("Хочу знать точных дат поездки", "примерных дат поездки")]
    [InlineData("Назови ТОЧНУЮ ДАТУ переезда", "примерную дату переезда")]
    public void Exact_date_rewrite_preserves_the_topic_and_requests_an_approximate_interval(
        string question, string preservedTopic)
    {
        var result = QuestionValidationHeuristics.TryValidate(question);

        result.Should().NotBeNull();
        result!.Status.Should().Be(QuestionValidationStatus.NeedsRewrite);
        result.SuggestedQuestion.Should().Contain(preservedTopic)
            .And.Contain("ориентировочный диапазон в днях, неделях или месяцах")
            .And.Contain("как символический ориентир");
        result.SuggestedQuestion.Should().NotContain("Назови точную дату")
            .And.NotContain("о точной дате");
        AssertSuggestionIsReadyForInterpretation(result.SuggestedQuestion!);
    }

    [Theory]
    [InlineData("Когда я точно перееду?", "Когда я примерно перееду")]
    [InlineData("Когда точно состоится переезд?", "Когда примерно состоится переезд")]
    [InlineData("Точно когда состоится переезд?", "примерно когда состоится переезд")]
    [InlineData("Когда именно состоится переезд?", "Когда примерно состоится переезд")]
    [InlineData("Именно когда состоится переезд?", "примерно когда состоится переезд")]
    [InlineData("когда он напишет точную дату?", "когда он напишет примерную дату")]
    public void When_exactly_rewrite_preserves_the_event(string question, string preservedEvent)
    {
        var result = QuestionValidationHeuristics.TryValidate(question);

        result.Should().NotBeNull();
        result!.Status.Should().Be(QuestionValidationStatus.NeedsRewrite);
        result.SuggestedQuestion.Should().Contain(preservedEvent)
            .And.Contain("ориентировочный диапазон");
        AssertSuggestionIsReadyForInterpretation(result.SuggestedQuestion!);
    }

    [Theory]
    [InlineData("Когда примерно состоится переезд?")]
    [InlineData("В какой срок я могу ожидать предложение о работе?")]
    [InlineData("Когда он напишет?")]
    [InlineData("Назови примерную дату переезда")]
    [InlineData("Назови примерный срок переезда, без точной даты")]
    [InlineData("Когда примерно я перееду, без указания точной даты?")]
    [InlineData("Назови не точную дату, а примерный срок переезда")]
    [InlineData("Что меня ждёт без точной даты?")]
    public void Approximate_timing_and_ordinary_questions_are_unchanged(string question)
    {
        QuestionValidationHeuristics.TryValidate(question).Should().BeNull();
    }

    [Theory]
    [InlineData("Когда я точно выиграю в лотерею?")]
    [InlineData("Назови точную дату и номер выигрыша в лотерею")]
    [InlineData("Когда я гарантированно перееду, назови точную дату")]
    [InlineData("Когда точно я перееду со 100% уверенностью?")]
    [InlineData("Точную дату переезда хочу знать на сто процентов")]
    [InlineData("Когда точно переезд и какой номер дома?")]
    public void Unrelated_exact_facts_keep_the_existing_generic_rewrite(string question)
    {
        var result = QuestionValidationHeuristics.TryValidate(question);

        result.Should().NotBeNull();
        result!.Status.Should().Be(QuestionValidationStatus.NeedsRewrite);
        result.SuggestedQuestion.Should().Be(
            "На какие возможности и риски мне стоит обратить внимание в этой ситуации?");
    }

    [Theory]
    [InlineData("Как заставить его вернуться, назови точную дату?")]
    [InlineData("Когда точно можно отменить лечение?")]
    [InlineData("Назови точную дату, когда вложить все деньги")]
    public void Timing_does_not_bypass_rejection_rules(string question)
    {
        var result = QuestionValidationHeuristics.TryValidate(question);

        result.Should().NotBeNull();
        result!.Status.Should().Be(QuestionValidationStatus.Rejected);
    }

    [Theory]
    [InlineData("Когда он напишет, что он думает точно?")]
    [InlineData("Назови точную дату, проверить его телефон?")]
    public void Timing_does_not_preserve_surveillance_requests(string question)
    {
        var result = QuestionValidationHeuristics.TryValidate(question);

        result.Should().NotBeNull();
        result!.Status.Should().Be(QuestionValidationStatus.NeedsRewrite);
        result.SuggestedQuestion.Should().Be(
            "На что мне обратить внимание в этих отношениях и как бережно прояснить ситуацию?");
    }

    private static void AssertSuggestionIsReadyForInterpretation(string suggestion)
    {
        QuestionValidationHeuristics.TryValidate(suggestion).Should().BeNull();
        new AiPrivacyGateway().Prepare(suggestion, AiPrivacyOperation.TarotInterpretation)
            .CanSendExternally.Should().BeTrue();
    }
}
