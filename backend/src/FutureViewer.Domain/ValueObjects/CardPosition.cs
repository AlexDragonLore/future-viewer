namespace FutureViewer.Domain.ValueObjects;

public sealed class CardPosition
{
    public required int Index { get; init; }
    public required string Name { get; init; }
    public required string Meaning { get; init; }
}
