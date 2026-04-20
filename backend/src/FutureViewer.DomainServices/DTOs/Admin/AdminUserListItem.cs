using FutureViewer.Domain.Enums;

namespace FutureViewer.DomainServices.DTOs.Admin;

public sealed class AdminUserListItem
{
    public required Guid Id { get; init; }
    public required string Email { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required bool IsAdmin { get; init; }
    public required SubscriptionStatus SubscriptionStatus { get; init; }
    public DateTime? SubscriptionExpiresAt { get; init; }
    public long? TelegramChatId { get; init; }
    public required int TotalReadings { get; init; }
    public required int TotalFeedbacks { get; init; }
    public required int TotalScore { get; init; }
}
