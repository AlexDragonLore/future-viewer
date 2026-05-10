using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Interfaces;

namespace FutureViewer.Integration.Tests.Fixtures;

public sealed class StubMemoryExtractor : IAIMemoryExtractor
{
    public Task<IReadOnlyList<string>> ExtractAsync(MemoryExtractionContext context, CancellationToken ct = default)
    {
        IReadOnlyList<string> rules = context.Question.Contains("remember me", StringComparison.OrdinalIgnoreCase)
            ? new[] { "Пользователь просит запомнить тестовый факт." }
            : Array.Empty<string>();
        return Task.FromResult(rules);
    }
}
