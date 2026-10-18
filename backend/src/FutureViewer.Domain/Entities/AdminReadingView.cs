namespace FutureViewer.Domain.Entities;

public sealed class AdminReadingView
{
    public Guid AdminId { get; init; }
    public Guid ReadingId { get; init; }
    public DateTime ViewedAt { get; init; } = DateTime.UtcNow;
}
