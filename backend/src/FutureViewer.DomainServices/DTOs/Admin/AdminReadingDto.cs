using FutureViewer.Domain.Enums;
using System.Text.Json.Serialization;

namespace FutureViewer.DomainServices.DTOs.Admin;

public sealed class AdminReadingDto
{
    public required Guid Id { get; init; }
    public required Guid? UserId { get; init; }
    public string? UserEmail { get; init; }
    public AdminSubscriptionDto? Subscription { get; init; }
    public required string Question { get; init; }
    public bool HasInterpretation { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Interpretation { get; init; }
    public required SpreadType SpreadType { get; init; }
    public required DeckType DeckType { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? DeletedFromHistoryAt { get; init; }
    // Guest content retention deadline; independent of the guest access ticket.
    public DateTime? ExpiresAt { get; init; }
}

public sealed class AdminReadingInterpretationDto
{
    public required Guid Id { get; init; }
    public string? Interpretation { get; init; }
}

public sealed class AdminReadingListResult
{
    public required IReadOnlyList<AdminReadingDto> Items { get; init; }
    public required int Total { get; init; }
}
