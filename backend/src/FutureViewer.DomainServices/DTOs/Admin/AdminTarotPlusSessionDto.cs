using FutureViewer.Domain.Enums;

namespace FutureViewer.DomainServices.DTOs.Admin;

public sealed class AdminTarotPlusSessionDto
{
    public required Guid Id { get; init; }
    public required Guid UserId { get; init; }
    public string? UserEmail { get; init; }
    public required TarotPlusSessionStatus Status { get; init; }
    public required TarotPlusRoute Route { get; init; }
    public required string RouteLabel { get; init; }
    public required string CoreRequest { get; init; }
    public string? PreviewText { get; init; }
    public required bool HasReport { get; init; }
    public required int AnswerCount { get; init; }
    public required int IntakeAnswerCount { get; init; }
    public required int FollowUpsLeft { get; init; }
    public required decimal PriceRub { get; init; }
    public string? PaymentId { get; init; }
    public DateTime? PaidAt { get; init; }
    public string? AiModel { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
    public required DateTime ExpiresAt { get; init; }
}

public sealed class AdminTarotPlusSessionListResult
{
    public required IReadOnlyList<AdminTarotPlusSessionDto> Items { get; init; }
    public required int Total { get; init; }
}
