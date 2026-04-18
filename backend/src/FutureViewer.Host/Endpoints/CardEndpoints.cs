using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.Interfaces;
using FutureViewer.DomainServices.Services;

namespace FutureViewer.Host.Endpoints;

public static class CardEndpoints
{
    public static IEndpointRouteBuilder MapCards(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cards").WithTags("Cards");

        group.MapGet("/", async (ICardDeck deck, CancellationToken ct) =>
        {
            var all = await deck.GetAllAsync(ct);
            return Results.Ok(all.Select(ToDto));
        });

        group.MapGet("/spreads", () =>
        {
            var spreads = new[] { Spread.SingleCard, Spread.ThreeCard, Spread.CelticCross };
            return Results.Ok(spreads.Select(s => new
            {
                type = s.Type,
                name = s.Name,
                cardCount = s.CardCount,
                positions = s.Positions
            }));
        });

        group.MapGet("/glossary", async (CardDeckService service, CancellationToken ct) =>
        {
            var glossary = await service.GetGlossaryAsync(ct);
            return Results.Ok(glossary);
        });

        group.MapGet("/{id:int}", async (int id, CardDeckService service, CancellationToken ct) =>
        {
            var card = await service.GetCardDetailAsync(id, ct);
            return card is null ? Results.NotFound() : Results.Ok(card);
        });

        return app;
    }

    private static object ToDto(TarotCard c) => new
    {
        id = c.Id,
        name = c.Name,
        suit = c.Suit,
        number = c.Number,
        descriptionUpright = c.DescriptionUpright,
        descriptionReversed = c.DescriptionReversed,
        imagePath = c.ImagePath
    };
}
