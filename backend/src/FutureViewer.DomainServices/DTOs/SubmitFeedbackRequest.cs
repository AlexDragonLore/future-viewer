namespace FutureViewer.DomainServices.DTOs;

public sealed class SubmitFeedbackRequest
{
    public required string Token { get; init; }
    public required string SelfReport { get; init; }
}
