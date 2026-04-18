using FutureViewer.Domain.Entities;
using FutureViewer.Domain.ValueObjects;

namespace FutureViewer.DomainServices.Interfaces;

public interface IAIInterpreter
{
    string Model { get; }

    Task<InterpretationResult> InterpretAsync(
        Spread spread,
        string question,
        IReadOnlyList<ReadingCard> cards,
        CancellationToken ct = default);

    IAsyncEnumerable<string> InterpretStreamAsync(
        Spread spread,
        string question,
        IReadOnlyList<ReadingCard> cards,
        CancellationToken ct = default);
}
