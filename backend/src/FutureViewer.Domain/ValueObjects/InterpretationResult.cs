namespace FutureViewer.Domain.ValueObjects;

public sealed class InterpretationResult
{
    public required string Text { get; init; }
    public required string Model { get; init; }
    public required DateTime GeneratedAt { get; init; }
}
