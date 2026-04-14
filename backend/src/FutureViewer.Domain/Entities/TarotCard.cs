using FutureViewer.Domain.Enums;

namespace FutureViewer.Domain.Entities;

public class TarotCard
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public CardSuit Suit { get; set; }
    public int Number { get; set; }
    public string DescriptionUpright { get; set; } = string.Empty;
    public string DescriptionReversed { get; set; } = string.Empty;
    public string ImagePath { get; set; } = string.Empty;
}
