using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Interfaces;
using FutureViewer.DomainServices.Services;

namespace FutureViewer.Infrastructure.AI;

/// <summary>
/// Kept behind the existing interface for compatibility, but intentionally performs
/// only local deterministic validation. A user question must never be sent to an AI
/// merely to decide whether it is safe enough to send to an AI.
/// </summary>
public sealed class QuestionValidationInterpreter : IAIQuestionValidator
{
    private readonly IAiPrivacyGateway _privacyGateway;

    public QuestionValidationInterpreter(IAiPrivacyGateway privacyGateway)
    {
        _privacyGateway = privacyGateway;
    }

    public Task<QuestionValidationResult> ValidateAsync(string question, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var decision = _privacyGateway.Prepare(question, AiPrivacyOperation.QuestionValidation);
        if (!decision.CanSendExternally)
        {
            return Task.FromResult(new QuestionValidationResult
            {
                Status = QuestionValidationStatus.Rejected,
                Reason = decision.UserMessage,
                SuggestedQuestion = null,
                BlockCode = decision.ReasonCode,
                SafeResponse = decision.SafeResponse
            });
        }

        var local = QuestionValidationHeuristics.TryValidate(decision.SafeText);
        return Task.FromResult(local ?? new QuestionValidationResult
        {
            Status = QuestionValidationStatus.Accepted,
            Reason = "Вопрос прошёл локальную проверку.",
            SuggestedQuestion = null
        });
    }
}
