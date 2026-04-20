using FutureViewer.Domain.Enums;

namespace FutureViewer.Domain.Entities;

public sealed class User
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Email { get; init; }
    public required string PasswordHash { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public bool IsAdmin { get; set; } = false;

    public bool IsEmailVerified { get; set; } = false;
    public string? EmailVerificationToken { get; set; }
    public DateTime? EmailVerificationSentAt { get; set; }

    public SubscriptionStatus SubscriptionStatus { get; set; } = SubscriptionStatus.None;
    public DateTime? SubscriptionExpiresAt { get; set; }
    public string? YukassaSubscriptionId { get; set; }

    public long? TelegramChatId { get; set; }
    public string? TelegramLinkToken { get; set; }

    public ICollection<Reading> Readings { get; init; } = new List<Reading>();
    public ICollection<ReadingFeedback> Feedbacks { get; init; } = new List<ReadingFeedback>();
    public ICollection<UserAchievement> Achievements { get; init; } = new List<UserAchievement>();
}
