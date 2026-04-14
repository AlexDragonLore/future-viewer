using FutureViewer.Domain.Enums;

namespace FutureViewer.DomainServices.DTOs;

public sealed record ReadingResult(
    Guid Id,
    SpreadType SpreadType,
    string SpreadName,
    string Question,
    DateTime CreatedAt,
    IReadOnlyList<ReadingCardDto> Cards,
    string? Interpretation);
