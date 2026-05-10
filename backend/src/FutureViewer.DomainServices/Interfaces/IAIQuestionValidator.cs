using FutureViewer.DomainServices.DTOs;

namespace FutureViewer.DomainServices.Interfaces;

public interface IAIQuestionValidator
{
    Task<QuestionValidationResult> ValidateAsync(string question, CancellationToken ct = default);
}
