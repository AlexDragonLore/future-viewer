using FutureViewer.Domain.Enums;

namespace FutureViewer.DomainServices.DTOs;

public sealed class CardGlossaryDto
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required string NameEn { get; init; }
    public required CardSuit Suit { get; init; }
    public required int Number { get; init; }
    public required string ImagePath { get; init; }
    public required string DescriptionUpright { get; init; }
    public required string DescriptionReversed { get; init; }
    public required string ShortUpright { get; init; }
    public required string ShortReversed { get; init; }
    public required IReadOnlyList<string> UprightKeywords { get; init; }
    public required IReadOnlyList<string> ReversedKeywords { get; init; }
    public required SuggestedTone SuggestedTone { get; init; }
    public required IReadOnlyList<string> Aliases { get; init; }
    public required IReadOnlyList<DeckVariantDto> DeckVariants { get; init; }
}
