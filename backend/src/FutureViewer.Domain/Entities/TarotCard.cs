using FutureViewer.Domain.Enums;

namespace FutureViewer.Domain.Entities;

public sealed class TarotCard
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required CardSuit Suit { get; init; }
    public required int Number { get; init; }
    public required string DescriptionUpright { get; init; }
    public required string DescriptionReversed { get; init; }
    public required string ImagePath { get; init; }
}
