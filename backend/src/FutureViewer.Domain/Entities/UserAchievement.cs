namespace FutureViewer.Domain.Entities;

public sealed class UserAchievement
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid UserId { get; init; }
    public User? User { get; init; }
    public required Guid AchievementId { get; init; }
    public Achievement? Achievement { get; init; }
    public DateTime UnlockedAt { get; init; } = DateTime.UtcNow;
}
