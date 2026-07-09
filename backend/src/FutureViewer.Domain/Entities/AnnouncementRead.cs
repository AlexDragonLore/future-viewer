namespace FutureViewer.Domain.Entities;

public sealed class AnnouncementRead
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid UserId { get; init; }
    public User? User { get; init; }
    public required Guid AnnouncementId { get; init; }
    public Announcement? Announcement { get; init; }
    public DateTime ReadAt { get; init; } = DateTime.UtcNow;
}
