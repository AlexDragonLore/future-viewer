using FutureViewer.Domain.Entities;
using FutureViewer.Domain.ValueObjects;
using FutureViewer.DomainServices.Interfaces;

namespace FutureViewer.Integration.Tests.Fixtures;

public sealed class StubAIInterpreter : IAIInterpreter
{
    public Task<InterpretationResult> InterpretAsync(Reading reading, Spread spread, CancellationToken ct = default)
    {
        return Task.FromResult(new InterpretationResult(
            $"Stub interpretation for {spread.Name} with {reading.Cards.Count} cards",
            "stub-model",
            DateTime.UtcNow));
    }
}
