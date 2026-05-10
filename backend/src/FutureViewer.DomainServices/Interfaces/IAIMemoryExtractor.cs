using FutureViewer.DomainServices.DTOs;

namespace FutureViewer.DomainServices.Interfaces;

public interface IAIMemoryExtractor
{
    Task<IReadOnlyList<string>> ExtractAsync(MemoryExtractionContext context, CancellationToken ct = default);
}
