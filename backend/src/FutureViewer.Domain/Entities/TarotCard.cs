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

    public string NameEn { get; set; } = string.Empty;
    public List<string> UprightKeywords { get; set; } = new();
    public List<string> ReversedKeywords { get; set; } = new();
    public string ShortUpright { get; set; } = string.Empty;
    public string ShortReversed { get; set; } = string.Empty;
    public SuggestedTone SuggestedTone { get; set; } = SuggestedTone.Neutral;
    public List<string>? Aliases { get; set; }

    public ICollection<DeckVariant> DeckVariants { get; init; } = new List<DeckVariant>();
}
