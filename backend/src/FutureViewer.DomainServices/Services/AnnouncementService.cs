using FutureViewer.Domain.Entities;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Exceptions;
using FutureViewer.DomainServices.Interfaces;

namespace FutureViewer.DomainServices.Services;

public sealed class AnnouncementService
{
    private readonly IAnnouncementRepository _announcements;

    public AnnouncementService(IAnnouncementRepository announcements)
    {
        _announcements = announcements;
    }

    public async Task<IReadOnlyList<AnnouncementDto>> GetUnreadAsync(Guid userId, CancellationToken ct = default)
    {
        var announcements = await _announcements.GetUnreadAsync(userId, ct);
        return announcements.Select(Map).ToList();
    }

    public async Task MarkReadAsync(Guid userId, Guid announcementId, CancellationToken ct = default)
    {
        var marked = await _announcements.MarkReadAsync(userId, announcementId, ct);
        if (!marked)
            throw new NotFoundException("Announcement not found");
    }

    private static AnnouncementDto Map(Announcement announcement) => new()
    {
        Id = announcement.Id,
        Code = announcement.Code,
        Title = announcement.Title,
        Body = announcement.Body,
        PublishedAt = announcement.PublishedAt
    };
}
