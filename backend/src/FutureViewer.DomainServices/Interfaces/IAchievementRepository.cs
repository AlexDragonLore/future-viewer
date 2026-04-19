using FutureViewer.Domain.Entities;

namespace FutureViewer.DomainServices.Interfaces;

public interface IAchievementRepository
{
    Task<IReadOnlyList<Achievement>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<UserAchievement>> GetByUserAsync(Guid userId, CancellationToken ct = default);
    Task<UserAchievement?> GrantAsync(UserAchievement userAchievement, CancellationToken ct = default);
}
