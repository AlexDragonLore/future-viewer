using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;

namespace FutureViewer.DomainServices.Interfaces;

public interface ICardDeck
{
    Task<IReadOnlyList<TarotCard>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TarotCard>> GetAllWithVariantsAsync(CancellationToken ct = default);
    Task<TarotCard?> GetByIdWithVariantsAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyDictionary<int, string>> GetVariantNotesAsync(
        DeckType deckType,
        IReadOnlyCollection<int> cardIds,
        CancellationToken ct = default);
}
