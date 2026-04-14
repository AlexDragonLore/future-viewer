namespace FutureViewer.DomainServices.DTOs;

public sealed class InterpretationResponse
{
    public required string Text { get; init; }
    public required string Model { get; init; }
}
