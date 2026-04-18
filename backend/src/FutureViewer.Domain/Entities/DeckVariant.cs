using FutureViewer.Domain.Enums;

namespace FutureViewer.Domain.Entities;

public sealed class DeckVariant
{
    public int Id { get; init; }
    public required int CardId { get; init; }
    public TarotCard? Card { get; init; }
    public required DeckType DeckType { get; init; }
    public required string VariantNote { get; init; }
}
