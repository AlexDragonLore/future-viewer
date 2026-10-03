namespace FutureViewer.DomainServices.DTOs;

public enum AiPrivacyOperation
{
    QuestionValidation,
    TarotInterpretation,
    FeedbackScoring,
    MemoryExtraction
}

public enum AiPrivacyDisposition
{
    Allowed,
    Redacted,
    Blocked
}

public sealed class AiPrivacyDecision
{
    public required Guid RequestId { get; init; }
    public required AiPrivacyOperation Operation { get; init; }
    public required AiPrivacyDisposition Disposition { get; init; }
    public required string SafeText { get; init; }
    public required string ReasonCode { get; init; }
    public required string UserMessage { get; init; }
    public string? SafeResponse { get; init; }

    public bool CanSendExternally => Disposition is not AiPrivacyDisposition.Blocked;
}
