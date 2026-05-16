using FluentAssertions;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Services;

namespace FutureViewer.DomainServices.Tests;

public sealed class QuestionValidationHeuristicsTests
{
    [Theory]
    [InlineData("ываыва")]
    [InlineData("asdf")]
    [InlineData("как заставить его вернуться?")]
    [InlineData("есть ли у меня рак?")]
    public void TryValidate_rejects_obviously_bad_questions(string question)
    {
        var result = QuestionValidationHeuristics.TryValidate(question);

        result.Should().NotBeNull();
        result!.Status.Should().Be(QuestionValidationStatus.Rejected);
        result.SuggestedQuestion.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("что будет?")]
    [InlineData("когда он напишет точную дату?")]
    [InlineData("любовь")]
    public void TryValidate_requests_rewrite_for_vague_or_exact_questions(string question)
    {
        var result = QuestionValidationHeuristics.TryValidate(question);

        result.Should().NotBeNull();
        result!.Status.Should().Be(QuestionValidationStatus.NeedsRewrite);
        result.SuggestedQuestion.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void TryValidate_allows_normal_questions_to_ai_validator()
    {
        var result = QuestionValidationHeuristics.TryValidate("На что мне обратить внимание сегодня в работе?");

        result.Should().BeNull();
    }
}
