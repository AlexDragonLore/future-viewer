namespace FutureViewer.DomainServices.DTOs;

public sealed class AnnouncementDto
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required string Title { get; init; }
    public required string Body { get; init; }
    public required DateTime PublishedAt { get; init; }
}
