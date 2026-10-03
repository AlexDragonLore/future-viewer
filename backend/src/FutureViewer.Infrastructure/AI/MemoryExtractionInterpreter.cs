using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Interfaces;

namespace FutureViewer.Infrastructure.AI;

/// <summary>
/// Compatibility adapter. Automatic long-term memory extraction remains disabled
/// until a separately reviewed structured, consent-aware local design exists.
/// </summary>
[Obsolete("Automatic memory extraction is disabled for privacy reasons.")]
public sealed class MemoryExtractionInterpreter : IAIMemoryExtractor
{
    public Task<IReadOnlyList<string>> ExtractAsync(
        MemoryExtractionContext context,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _ = context;
        return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }
}
