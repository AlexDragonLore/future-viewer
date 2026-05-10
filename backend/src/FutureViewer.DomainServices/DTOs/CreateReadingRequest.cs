using FutureViewer.Domain.Enums;

namespace FutureViewer.DomainServices.DTOs;

public sealed class CreateReadingRequest
{
    public required SpreadType SpreadType { get; init; }
    public required string Question { get; init; }
    public DeckType DeckType { get; init; } = DeckType.RWS;
    public DateOnly? ClientDate { get; init; }
    public string? ClientTimeZone { get; init; }
}
