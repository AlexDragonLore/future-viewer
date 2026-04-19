using FutureViewer.Domain.Enums;

namespace FutureViewer.Domain.Entities;

public sealed class User
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Email { get; init; }
    public required string PasswordHash { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public SubscriptionStatus SubscriptionStatus { get; set; } = SubscriptionStatus.None;
    public DateTime? SubscriptionExpiresAt { get; set; }
    public string? YukassaSubscriptionId { get; set; }

    public ICollection<Reading> Readings { get; init; } = new List<Reading>();
}
