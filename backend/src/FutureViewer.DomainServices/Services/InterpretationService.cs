using FutureViewer.Domain.Entities;
using FutureViewer.Domain.ValueObjects;
using FutureViewer.DomainServices.Interfaces;

namespace FutureViewer.DomainServices.Services;

public sealed class InterpretationService
{
    private readonly IAIInterpreter _interpreter;

    public InterpretationService(IAIInterpreter interpreter)
    {
        _interpreter = interpreter;
    }

    public string Model => _interpreter.Model;

    public Task<InterpretationResult> InterpretAsync(
        Spread spread,
        string question,
        IReadOnlyList<ReadingCard> cards,
        CancellationToken ct = default)
    {
        return _interpreter.InterpretAsync(spread, question, cards, ct);
    }

    public IAsyncEnumerable<string> InterpretStreamAsync(
        Spread spread,
        string question,
        IReadOnlyList<ReadingCard> cards,
        CancellationToken ct = default)
    {
        return _interpreter.InterpretStreamAsync(spread, question, cards, ct);
    }
}
