using FutureViewer.Domain.Entities;

namespace FutureViewer.DomainServices.Interfaces;

public interface IAchievementRepository
{
    Task<IReadOnlyList<Achievement>> GetAllAsync(CancellationToken ct = default);
    Task<Achievement?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<IReadOnlyList<UserAchievement>> GetByUserAsync(Guid userId, CancellationToken ct = default);
    Task<UserAchievement?> GrantAsync(UserAchievement userAchievement, CancellationToken ct = default);
    Task<bool> RevokeAsync(Guid userId, Guid achievementId, CancellationToken ct = default);
}
