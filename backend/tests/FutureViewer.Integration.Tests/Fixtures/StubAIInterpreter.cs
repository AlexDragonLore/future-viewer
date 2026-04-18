using FutureViewer.Domain.Entities;
using FutureViewer.Domain.ValueObjects;
using FutureViewer.DomainServices.Interfaces;

namespace FutureViewer.Integration.Tests.Fixtures;

public sealed class StubAIInterpreter : IAIInterpreter
{
    public string Model => "stub-model";

    public Task<InterpretationResult> InterpretAsync(
        Spread spread,
        string question,
        IReadOnlyList<ReadingCard> cards,
        CancellationToken ct = default)
    {
        return Task.FromResult(new InterpretationResult
        {
            Text = $"Stub interpretation for {spread.Name} with {cards.Count} cards",
            Model = Model,
            GeneratedAt = DateTime.UtcNow
        });
    }

    public async IAsyncEnumerable<string> InterpretStreamAsync(
        Spread spread,
        string question,
        IReadOnlyList<ReadingCard> cards,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        yield return $"Stub interpretation for {spread.Name} with {cards.Count} cards";
        await Task.CompletedTask;
    }
}
