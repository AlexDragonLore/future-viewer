using FutureViewer.DomainServices.Exceptions;
using FutureViewer.DomainServices.Services;

namespace FutureViewer.Host.Endpoints;

public static class AnnouncementEndpoints
{
    public static IEndpointRouteBuilder MapAnnouncements(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/announcements")
            .WithTags("Announcements")
            .RequireAuthorization();

        group.MapGet("/unread", async (
            AnnouncementService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = ctx.User.GetUserId()
                ?? throw new UnauthorizedException("Authentication required");
            return Results.Ok(await service.GetUnreadAsync(userId, ct));
        });

        group.MapPost("/{id:guid}/read", async (
            Guid id,
            AnnouncementService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = ctx.User.GetUserId()
                ?? throw new UnauthorizedException("Authentication required");
            await service.MarkReadAsync(userId, id, ct);
            return Results.NoContent();
        });

        return app;
    }
}
