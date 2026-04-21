using FutureViewer.DomainServices.Exceptions;
using FutureViewer.DomainServices.Services;

namespace FutureViewer.Host.Endpoints;

public static class TelegramEndpoints
{
    public static IEndpointRouteBuilder MapTelegram(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/telegram").WithTags("Telegram");

        group.MapPost("/link", async (
            TelegramLinkService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = ctx.User.GetUserId()
                ?? throw new UnauthorizedException("Authentication required");
            var response = await service.GenerateLinkAsync(userId, ct);
            return Results.Ok(response);
        }).RequireAuthorization();

        group.MapDelete("/link", async (
            TelegramLinkService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = ctx.User.GetUserId()
                ?? throw new UnauthorizedException("Authentication required");
            await service.UnlinkAsync(userId, ct);
            return Results.NoContent();
        }).RequireAuthorization();

        group.MapGet("/status", async (
            TelegramLinkService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = ctx.User.GetUserId()
                ?? throw new UnauthorizedException("Authentication required");
            var isLinked = await service.IsLinkedAsync(userId, ct);
            return Results.Ok(new { isLinked });
        }).RequireAuthorization();

        return app;
    }
}
