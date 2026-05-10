namespace FutureViewer.DomainServices.DTOs;

public enum QuestionValidationStatus
{
    Accepted,
    NeedsRewrite,
    Rejected
}

public sealed class QuestionValidationResult
{
    public required QuestionValidationStatus Status { get; init; }
    public required string Reason { get; init; }
    public string? SuggestedQuestion { get; init; }
}
