using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Interfaces;
using FutureViewer.DomainServices.Services;
using Microsoft.Extensions.Logging;

namespace FutureViewer.Infrastructure.AI;

public sealed class DevelopmentQuestionValidator : IAIQuestionValidator
{
    private readonly ILogger<DevelopmentQuestionValidator> _logger;

    public DevelopmentQuestionValidator(ILogger<DevelopmentQuestionValidator> logger)
    {
        _logger = logger;
    }

    public Task<QuestionValidationResult> ValidateAsync(string question, CancellationToken ct = default)
    {
        var localDecision = QuestionValidationHeuristics.TryValidate(question);
        if (localDecision is not null)
            return Task.FromResult(localDecision);

        _logger.LogInformation("OpenAI:ApiKey is not configured; accepting question with development validator");
        return Task.FromResult(new QuestionValidationResult
        {
            Status = QuestionValidationStatus.Accepted,
            Reason = "Вопрос принят локальной dev-проверкой.",
            SuggestedQuestion = null
        });
    }
}
