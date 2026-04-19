namespace FutureViewer.DomainServices.DTOs;

public sealed class AchievementDto
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string IconPath { get; init; }
    public required int SortOrder { get; init; }
    public DateTime? UnlockedAt { get; init; }
}
