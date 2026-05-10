using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Interfaces;

namespace FutureViewer.Integration.Tests.Fixtures;

public sealed class StubQuestionValidator : IAIQuestionValidator
{
    public Task<QuestionValidationResult> ValidateAsync(string question, CancellationToken ct = default)
    {
        var result = question.Contains("needs rewrite", StringComparison.OrdinalIgnoreCase)
            ? new QuestionValidationResult
            {
                Status = QuestionValidationStatus.NeedsRewrite,
                Reason = "Вопрос лучше уточнить.",
                SuggestedQuestion = "На что мне обратить внимание в этой ситуации?"
            }
            : question.Contains("rejected", StringComparison.OrdinalIgnoreCase)
                ? new QuestionValidationResult
                {
                    Status = QuestionValidationStatus.Rejected,
                    Reason = "Вопрос не подходит для расклада.",
                    SuggestedQuestion = null
                }
                : new QuestionValidationResult
                {
                    Status = QuestionValidationStatus.Accepted,
                    Reason = "ok",
                    SuggestedQuestion = null
                };

        return Task.FromResult(result);
    }
}
