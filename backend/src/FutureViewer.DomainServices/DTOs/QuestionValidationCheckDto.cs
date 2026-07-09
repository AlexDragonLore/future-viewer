namespace FutureViewer.DomainServices.DTOs;

public sealed class QuestionValidationCheckDto
{
    public required string Status { get; init; }
    public required string Reason { get; init; }
    public string? SuggestedQuestion { get; init; }
    public required string Message { get; init; }
    public required bool CanContinue { get; init; }
    public required bool RequiresSubscription { get; init; }
}
