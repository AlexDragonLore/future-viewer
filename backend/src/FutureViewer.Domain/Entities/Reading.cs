using FutureViewer.Domain.Enums;

namespace FutureViewer.Domain.Entities;

public sealed class Reading
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid? UserId { get; init; }
    public User? User { get; init; }
    public required SpreadType SpreadType { get; init; }
    public required string Question { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public string? AiInterpretation { get; init; }
    public string? AiModel { get; init; }

    public ICollection<ReadingCard> Cards { get; init; } = new List<ReadingCard>();
}
