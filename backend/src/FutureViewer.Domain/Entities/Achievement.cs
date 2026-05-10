namespace FutureViewer.Domain.Entities;

public sealed class Achievement
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Code { get; init; }
    public required string NameRu { get; init; }
    public required string DescriptionRu { get; init; }
    public required string IconPath { get; init; }
    public int SortOrder { get; init; }
    public int Points { get; set; }

    public ICollection<UserAchievement> UserAchievements { get; init; } = new List<UserAchievement>();
}
