namespace FutureViewer.Domain.Entities;

public class ReadingCard
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReadingId { get; set; }
    public Reading Reading { get; set; } = null!;
    public int CardId { get; set; }
    public TarotCard Card { get; set; } = null!;
    public int Position { get; set; }
    public bool IsReversed { get; set; }
}
