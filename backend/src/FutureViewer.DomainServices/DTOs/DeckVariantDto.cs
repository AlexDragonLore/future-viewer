using FutureViewer.Domain.Enums;

namespace FutureViewer.DomainServices.DTOs;

public sealed class DeckVariantDto
{
    public required DeckType DeckType { get; init; }
    public required string VariantNote { get; init; }
}
