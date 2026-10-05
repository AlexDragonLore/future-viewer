using FluentAssertions;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Services;

namespace FutureViewer.DomainServices.Tests;

public sealed class AiPrivacyGatewayTests
{
    private readonly AiPrivacyGateway _sut = new();

    [Theory]
    [InlineData("Напиши мне на ivan.petrov@example.com", "email")]
    [InlineData("Позвони мне +7 (999) 123-45-67", "phone")]
    [InlineData("У меня диагноз диабет, что делать?", "special_category")]
    [InlineData("Что думает Иван Петров обо мне?", "third_party_or_name")]
    [InlineData("Моей дочери 14 лет, что с ней будет?", "minor_data")]
    [InlineData("Мой адрес: улица Ленина, дом 10", "address")]
    [InlineData("Как пережить беременность?", "special_category")]
    [InlineData("У меня депрессия", "special_category")]
    [InlineData("Как моя национальность влияет на будущее?", "special_category")]
    [InlineData("У меня политические убеждения, как их защитить?", "special_category")]
    [InlineData("Что меня ждёт после болезни?", "special_category")]
    [InlineData("Как преодолеть рак?", "special_category")]
    [InlineData("Что будет у моего мужа?", "third_party_or_name")]
    [InlineData("Что ожидает мою жену?", "third_party_or_name")]
    public void Prepare_blocks_identifying_or_sensitive_question(string question, string code)
    {
        var result = _sut.Prepare(question, AiPrivacyOperation.TarotInterpretation);

        result.Disposition.Should().Be(AiPrivacyDisposition.Blocked);
        result.CanSendExternally.Should().BeFalse();
        result.SafeText.Should().BeEmpty();
        result.ReasonCode.Should().Be(code);
        result.UserMessage.Should().Contain("персональным или чувствительным данным");
    }

    [Theory]
    [InlineData("Я хочу покончить с собой", "self_harm", "112")]
    [InlineData("У меня признаки инсульта, ждать ли?", "urgent_medical", "103")]
    [InlineData("Мне отменить лекарство?", "medication", "врач")]
    [InlineData("Взять кредит и купить криптовалюту?", "financial_decision", "финансов")]
    [InlineData("Подписать договор прямо сейчас?", "legal_decision", "юрид")]
    [InlineData("Думаю о самоубийстве", "self_harm", "112")]
    [InlineData("Думаю вскрыть вены", "self_harm", "112")]
    [InlineData("Как остановить домашнее насилие?", "violence", "112")]
    [InlineData("Как скрыть преступление?", "crime", "адвокату")]
    [InlineData("Стоит ли купить акции?", "financial_decision", "финансов")]
    [InlineData("Стоит ли инвестировать сейчас?", "financial_decision", "финансов")]
    [InlineData("Подписать соглашение?", "legal_decision", "юрид")]
    [InlineData("Стоит ли подать заявление?", "legal_decision", "юрид")]
    public void Prepare_routes_high_stakes_question_to_local_safe_response(
        string question,
        string code,
        string expectedText)
    {
        var result = _sut.Prepare(question, AiPrivacyOperation.TarotInterpretation);

        result.CanSendExternally.Should().BeFalse();
        result.ReasonCode.Should().Be(code);
        result.SafeResponse.Should().Contain(expectedText);
    }

    [Fact]
    public void Prepare_normalizes_safe_question_and_creates_unlinkable_request_id()
    {
        var first = _sut.Prepare("  Какие   возможности\nмне стоит рассмотреть?  ", AiPrivacyOperation.TarotInterpretation);
        var second = _sut.Prepare("Какие возможности мне стоит рассмотреть?", AiPrivacyOperation.TarotInterpretation);

        first.CanSendExternally.Should().BeTrue();
        first.SafeText.Should().Be("Какие возможности мне стоит рассмотреть?");
        first.RequestId.Should().NotBe(Guid.Empty);
        second.RequestId.Should().NotBe(first.RequestId);
    }

    [Fact]
    public void Prepare_blocks_valid_payment_card_number()
    {
        var result = _sut.Prepare("Карта 4111 1111 1111 1111", AiPrivacyOperation.TarotInterpretation);

        result.CanSendExternally.Should().BeFalse();
        result.ReasonCode.Should().Be("bank_details");
    }

    [Theory]
    [InlineData("Что он чувствует ко мне?")]
    [InlineData("Как будут развиваться наши отношения?")]
    [InlineData("Мне грустно после расставания. Что поможет двигаться дальше?")]
    public void Prepare_allows_ordinary_relationship_questions_without_identifying_or_sensitive_details(string question)
    {
        var result = _sut.Prepare(question, AiPrivacyOperation.TarotInterpretation);

        result.CanSendExternally.Should().BeTrue();
        result.Disposition.Should().Be(AiPrivacyDisposition.Allowed);
        result.SafeText.Should().Be(question);
        result.SafeResponse.Should().BeNull();
    }

    [Theory]
    [InlineData("Как укрепить характер и раскрыть таланты?")]
    [InlineData("Как разнообразить повседневную практику?")]
    [InlineData("Как найти вдохновение для творчества?")]
    public void Prepare_keeps_non_sensitive_words_with_similar_fragments_allowed(string question)
    {
        _sut.Prepare(question, AiPrivacyOperation.TarotInterpretation)
            .CanSendExternally.Should().BeTrue();
    }
}
