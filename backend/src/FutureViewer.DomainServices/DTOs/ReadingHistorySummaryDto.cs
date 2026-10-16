using FutureViewer.Domain.Enums;

namespace FutureViewer.DomainServices.DTOs;

public sealed class ReadingHistorySummaryDto
{
    public const int PreviewLength = 240;
    public required Guid Id { get; init; }
    public required SpreadType SpreadType { get; init; }
    public string SpreadName { get; set; } = string.Empty;
    public required string Question { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DeckType DeckType { get; init; }
    public string? InterpretationPreview { get; init; }
}

public sealed class ReadingHistoryPageDto
{
    public required IReadOnlyList<ReadingHistorySummaryDto> Items { get; init; }
    public required int Total { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
}
