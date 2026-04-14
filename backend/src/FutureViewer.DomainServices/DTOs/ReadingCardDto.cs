namespace FutureViewer.DomainServices.DTOs;

public sealed record ReadingCardDto(
    int Position,
    string PositionName,
    string PositionMeaning,
    int CardId,
    string CardName,
    string ImagePath,
    bool IsReversed,
    string Meaning);
