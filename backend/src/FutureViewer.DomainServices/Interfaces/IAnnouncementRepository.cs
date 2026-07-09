using FutureViewer.Domain.Entities;

namespace FutureViewer.DomainServices.Interfaces;

public interface IAnnouncementRepository
{
    Task<IReadOnlyList<Announcement>> GetUnreadAsync(Guid userId, CancellationToken ct = default);
    Task<bool> MarkReadAsync(Guid userId, Guid announcementId, CancellationToken ct = default);
}
