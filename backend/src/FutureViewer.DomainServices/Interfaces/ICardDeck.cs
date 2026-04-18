using FutureViewer.Domain.Entities;

namespace FutureViewer.DomainServices.Interfaces;

public interface ICardDeck
{
    Task<IReadOnlyList<TarotCard>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TarotCard>> GetAllWithVariantsAsync(CancellationToken ct = default);
    Task<TarotCard?> GetByIdWithVariantsAsync(int id, CancellationToken ct = default);
}
