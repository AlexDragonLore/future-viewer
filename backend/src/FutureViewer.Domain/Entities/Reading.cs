using FutureViewer.Domain.Enums;

namespace FutureViewer.Domain.Entities;

public class Reading
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }
    public User? User { get; set; }
    public SpreadType SpreadType { get; set; }
    public string Question { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? AiInterpretation { get; set; }
    public string? AiModel { get; set; }

    public ICollection<ReadingCard> Cards { get; set; } = new List<ReadingCard>();
}
