namespace FutureViewer.DomainServices.DTOs;

public sealed class MemoryExtractionContext
{
    public required string Question { get; init; }
    public required string Interpretation { get; init; }
    public required UserPromptContext PromptContext { get; init; }
}
