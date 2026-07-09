using FutureViewer.DomainServices.DTOs;

namespace FutureViewer.DomainServices.Services;

public static class QuestionValidationPolicy
{
    public const string SubscriberWarningMessage =
        "По такому запросу обычно не гадают. Вы уверены, что хотите продолжить?";

    public const string SubscriptionRequiredMessage =
        "На такие запросы можно ответить только с подпиской.";

    public static QuestionValidationCheckDto BuildCheck(
        string question,
        QuestionValidationResult validation,
        bool hasActiveSubscription)
    {
        var status = ToWireStatus(validation.Status);
        if (validation.Status == QuestionValidationStatus.Accepted)
        {
            return new QuestionValidationCheckDto
            {
                Status = status,
                Reason = validation.Reason,
                SuggestedQuestion = null,
                Message = validation.Reason,
                CanContinue = true,
                RequiresSubscription = false
            };
        }

        var suggestedQuestion = validation.SuggestedQuestion
            ?? QuestionValidationHeuristics.BuildFallbackSuggestion(question);

        return new QuestionValidationCheckDto
        {
            Status = status,
            Reason = validation.Reason,
            SuggestedQuestion = suggestedQuestion,
            Message = hasActiveSubscription ? SubscriberWarningMessage : SubscriptionRequiredMessage,
            CanContinue = hasActiveSubscription,
            RequiresSubscription = !hasActiveSubscription
        };
    }

    public static string ToWireStatus(QuestionValidationStatus status) => status switch
    {
        QuestionValidationStatus.NeedsRewrite => "needs_rewrite",
        QuestionValidationStatus.Rejected => "rejected",
        _ => "accepted"
    };
}
