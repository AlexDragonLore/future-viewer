namespace FutureViewer.Domain.Entities;

public sealed class ReadingCard
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ReadingId { get; init; }
    public Reading? Reading { get; init; }
    public required int CardId { get; init; }
    public required TarotCard Card { get; init; }
    public required int Position { get; init; }
    public required bool IsReversed { get; init; }
}
