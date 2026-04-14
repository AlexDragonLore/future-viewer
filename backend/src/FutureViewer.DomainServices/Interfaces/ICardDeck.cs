using FutureViewer.Domain.Entities;

namespace FutureViewer.DomainServices.Interfaces;

public interface ICardDeck
{
    Task<IReadOnlyList<TarotCard>> GetAllAsync(CancellationToken ct = default);
}
