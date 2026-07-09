using FutureViewer.Domain.Entities;
using FutureViewer.DomainServices.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FutureViewer.Infrastructure.Persistence.Repositories;

public sealed class AnnouncementRepository : IAnnouncementRepository
{
    private readonly AppDbContext _db;

    public AnnouncementRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Announcement>> GetUnreadAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var readIds = _db.AnnouncementReads
            .Where(r => r.UserId == userId)
            .Select(r => r.AnnouncementId);

        return await _db.Announcements
            .Where(a => a.IsActive && !readIds.Contains(a.Id))
            .OrderByDescending(a => a.PublishedAt)
            .ThenByDescending(a => a.Id)
            .ToListAsync(ct);
    }

    public async Task<bool> MarkReadAsync(
        Guid userId,
        Guid announcementId,
        CancellationToken ct = default)
    {
        var exists = await _db.Announcements
            .AnyAsync(a => a.Id == announcementId && a.IsActive, ct);
        if (!exists) return false;

        var alreadyRead = await _db.AnnouncementReads
            .AnyAsync(r => r.UserId == userId && r.AnnouncementId == announcementId, ct);
        if (alreadyRead) return true;

        await _db.AnnouncementReads.AddAsync(new AnnouncementRead
        {
            UserId = userId,
            AnnouncementId = announcementId
        }, ct);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
