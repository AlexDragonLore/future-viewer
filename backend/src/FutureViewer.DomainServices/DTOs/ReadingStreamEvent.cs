namespace FutureViewer.DomainServices.DTOs;

public abstract record ReadingStreamEvent
{
    public sealed record Cards(ReadingResult Reading) : ReadingStreamEvent;
    public sealed record Chunk(string Delta) : ReadingStreamEvent;
    public sealed record Done : ReadingStreamEvent;
}
