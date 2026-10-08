using FutureViewer.Domain.Enums;

namespace FutureViewer.Domain.Entities;

public sealed class User
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid PrivacySubjectId { get; init; } = Guid.NewGuid();
    public required string Email { get; init; }
    public required string PasswordHash { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public bool IsAdmin { get; set; } = false;

    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public int? BirthYear { get; set; }
    public bool IsAdultConfirmed { get; set; }
    public bool HistoryEnabled { get; set; } = true;
    public UserAccountStatus AccountStatus { get; set; } = UserAccountStatus.Active;
    public int SecurityVersion { get; set; } = 1;
    public DateTime? AccountDeletionRequestedAt { get; set; }

    public bool IsEmailVerified { get; set; } = false;
    public string? EmailVerificationToken { get; set; }
    public DateTime? EmailVerificationSentAt { get; set; }

    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetTokenExpiresAt { get; set; }

    public SubscriptionStatus SubscriptionStatus { get; set; } = SubscriptionStatus.None;
    public DateTime? SubscriptionExpiresAt { get; set; }
    public string? YukassaSubscriptionId { get; set; }
    public bool HasUsedIntroReading { get; set; }
    public DateTime? LastReadingAt { get; set; }


    public ICollection<Reading> Readings { get; init; } = new List<Reading>();
    public ICollection<ReadingFeedback> Feedbacks { get; init; } = new List<ReadingFeedback>();
    public ICollection<UserAchievement> Achievements { get; init; } = new List<UserAchievement>();
    public ICollection<UserMemoryRule> MemoryRules { get; init; } = new List<UserMemoryRule>();
    public ICollection<AnnouncementRead> AnnouncementReads { get; init; } = new List<AnnouncementRead>();
    public ICollection<UserConsent> Consents { get; init; } = new List<UserConsent>();
}
