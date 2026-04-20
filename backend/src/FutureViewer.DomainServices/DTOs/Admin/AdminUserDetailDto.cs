using FutureViewer.Domain.Enums;

namespace FutureViewer.DomainServices.DTOs.Admin;

public sealed class AdminUserDetailDto
{
    public required Guid Id { get; init; }
    public required string Email { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required bool IsAdmin { get; init; }
    public required SubscriptionStatus SubscriptionStatus { get; init; }
    public DateTime? SubscriptionExpiresAt { get; init; }
    public string? YukassaSubscriptionId { get; init; }
    public long? TelegramChatId { get; init; }
    public bool HasTelegramLinkToken { get; init; }
    public required int TotalReadings { get; init; }
    public required int TotalFeedbacks { get; init; }
    public required int TotalScore { get; init; }
    public required IReadOnlyList<AdminReadingSummary> RecentReadings { get; init; }
    public required IReadOnlyList<AdminFeedbackDto> RecentFeedbacks { get; init; }
    public required IReadOnlyList<AdminAchievementDto> Achievements { get; init; }
}

public sealed class AdminReadingSummary
{
    public required Guid Id { get; init; }
    public required string Question { get; init; }
    public required SpreadType SpreadType { get; init; }
    public required DeckType DeckType { get; init; }
    public required DateTime CreatedAt { get; init; }
}

public sealed class AdminAchievementDto
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required DateTime UnlockedAt { get; init; }
}
