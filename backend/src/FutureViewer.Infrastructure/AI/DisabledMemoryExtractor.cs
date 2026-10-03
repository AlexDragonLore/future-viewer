using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Interfaces;

namespace FutureViewer.Infrastructure.AI;

/// <summary>
/// Personal memory is opt-in and remains disabled until consent and an approved,
/// structured, non-sensitive schema are available.
/// </summary>
public sealed class DisabledMemoryExtractor : IAIMemoryExtractor
{
    public Task<IReadOnlyList<string>> ExtractAsync(
        MemoryExtractionContext context,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }
}
