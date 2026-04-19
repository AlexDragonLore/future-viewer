using FutureViewer.DomainServices.Exceptions;
using FutureViewer.DomainServices.Services;

namespace FutureViewer.Host.Endpoints;

public static class LeaderboardEndpoints
{
    public static IEndpointRouteBuilder MapLeaderboard(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/leaderboard").WithTags("Leaderboard");

        group.MapGet("/monthly", async (
            LeaderboardService service,
            int? year,
            int? month,
            int? take,
            CancellationToken ct) =>
        {
            var entries = await service.GetMonthlyAsync(year, month, take ?? 50, ct);
            return Results.Ok(entries);
        });

        group.MapGet("/alltime", async (
            LeaderboardService service,
            int? take,
            CancellationToken ct) =>
        {
            var entries = await service.GetAllTimeAsync(take ?? 50, ct);
            return Results.Ok(entries);
        });

        group.MapGet("/me", async (
            LeaderboardService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = ctx.User.GetUserId()
                ?? throw new UnauthorizedException("Authentication required");
            var summary = await service.GetUserSummaryAsync(userId, ct);
            return summary is null ? Results.NoContent() : Results.Ok(summary);
        }).RequireAuthorization();

        return app;
    }
}
