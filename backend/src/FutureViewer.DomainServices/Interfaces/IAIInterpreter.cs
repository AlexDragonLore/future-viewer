using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.Domain.ValueObjects;

namespace FutureViewer.DomainServices.Interfaces;

public interface IAIInterpreter
{
    string Model { get; }

    Task<InterpretationResult> InterpretAsync(
        Spread spread,
        string question,
        IReadOnlyList<ReadingCard> cards,
        DeckType deckType,
        IReadOnlyDictionary<int, string> variantNotes,
        CancellationToken ct = default);

    IAsyncEnumerable<string> InterpretStreamAsync(
        Spread spread,
        string question,
        IReadOnlyList<ReadingCard> cards,
        DeckType deckType,
        IReadOnlyDictionary<int, string> variantNotes,
        CancellationToken ct = default);
}
