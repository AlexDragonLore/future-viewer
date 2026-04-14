namespace FutureViewer.DomainServices.DTOs;

public sealed class ReadingCardDto
{
    public required int Position { get; init; }
    public required string PositionName { get; init; }
    public required string PositionMeaning { get; init; }
    public required int CardId { get; init; }
    public required string CardName { get; init; }
    public required string ImagePath { get; init; }
    public required bool IsReversed { get; init; }
    public required string Meaning { get; init; }
}
