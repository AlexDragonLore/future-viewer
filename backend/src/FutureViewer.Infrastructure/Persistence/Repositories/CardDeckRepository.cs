using FutureViewer.Domain.Entities;
using FutureViewer.DomainServices.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FutureViewer.Infrastructure.Persistence.Repositories;

public sealed class CardDeckRepository : ICardDeck
{
    private readonly AppDbContext _db;

    public CardDeckRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<TarotCard>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.TarotCards.AsNoTracking().ToListAsync(ct);
    }
}
