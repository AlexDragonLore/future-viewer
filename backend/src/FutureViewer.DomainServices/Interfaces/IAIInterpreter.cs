using FutureViewer.Domain.Entities;
using FutureViewer.Domain.ValueObjects;

namespace FutureViewer.DomainServices.Interfaces;

public interface IAIInterpreter
{
    Task<InterpretationResult> InterpretAsync(
        Spread spread,
        string question,
        IReadOnlyList<ReadingCard> cards,
        CancellationToken ct = default);
}
