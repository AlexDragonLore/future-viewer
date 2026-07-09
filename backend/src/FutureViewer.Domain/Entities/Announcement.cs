namespace FutureViewer.Domain.Entities;

public sealed class Announcement
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Code { get; init; }
    public required string Title { get; set; }
    public required string Body { get; set; }
    public DateTime PublishedAt { get; init; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    public ICollection<AnnouncementRead> Reads { get; init; } = new List<AnnouncementRead>();
}
