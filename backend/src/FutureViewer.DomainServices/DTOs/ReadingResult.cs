using FutureViewer.Domain.Enums;

namespace FutureViewer.DomainServices.DTOs;

public sealed class ReadingResult
{
    public required Guid Id { get; init; }
    public required SpreadType SpreadType { get; init; }
    public required string SpreadName { get; init; }
    public required string Question { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required IReadOnlyList<ReadingCardDto> Cards { get; init; }
    public string? Interpretation { get; init; }
}
