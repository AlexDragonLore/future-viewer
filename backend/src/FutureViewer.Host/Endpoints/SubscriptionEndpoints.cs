using System.Security.Claims;
using FutureViewer.DomainServices.Exceptions;
using FutureViewer.DomainServices.Services;

namespace FutureViewer.Host.Endpoints;

public static class SubscriptionEndpoints
{
    public static IEndpointRouteBuilder MapSubscription(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/subscription")
            .WithTags("Subscription")
            .RequireAuthorization();

        group.MapGet("/status", async (
            SubscriptionService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = GetUserId(ctx.User)
                ?? throw new UnauthorizedException("Authentication required");
            var status = await service.GetStatusAsync(userId, ct);
            return Results.Ok(status);
        });

        return app;
    }

    private static Guid? GetUserId(ClaimsPrincipal principal)
    {
        var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? principal.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
