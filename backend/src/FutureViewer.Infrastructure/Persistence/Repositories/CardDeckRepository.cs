using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
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

    public async Task<IReadOnlyList<TarotCard>> GetAllWithVariantsAsync(CancellationToken ct = default)
    {
        return await _db.TarotCards
            .AsNoTracking()
            .Include(c => c.DeckVariants)
            .OrderBy(c => c.Suit)
            .ThenBy(c => c.Number)
            .ToListAsync(ct);
    }

    public async Task<TarotCard?> GetByIdWithVariantsAsync(int id, CancellationToken ct = default)
    {
        return await _db.TarotCards
            .AsNoTracking()
            .Include(c => c.DeckVariants)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<IReadOnlyDictionary<int, string>> GetVariantNotesAsync(
        DeckType deckType,
        IReadOnlyCollection<int> cardIds,
        CancellationToken ct = default)
    {
        if (cardIds.Count == 0)
            return new Dictionary<int, string>();

        var variants = await _db.DeckVariants
            .AsNoTracking()
            .Where(v => v.DeckType == deckType && cardIds.Contains(v.CardId))
            .Select(v => new { v.CardId, v.VariantNote })
            .ToListAsync(ct);

        return variants.ToDictionary(v => v.CardId, v => v.VariantNote);
    }
}
