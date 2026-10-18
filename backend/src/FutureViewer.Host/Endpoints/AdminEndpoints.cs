using System.Security.Claims;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.DTOs.Admin;
using FutureViewer.DomainServices.Exceptions;
using FutureViewer.DomainServices.Services;

namespace FutureViewer.Host.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdmin(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin")
            .WithTags("Admin")
            .RequireAuthorization("Admin");

        MapReadingEndpoints(group);
        MapUserEndpoints(group);
        MapAchievementEndpoints(group);
        MapStatsEndpoints(group);

        return app;
    }

    private static void MapReadingEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/readings", async (
            Guid? userId,
            string? search,
            int? page,
            int? pageSize,
            bool? includeInterpretation,
            string? audience,
            bool? unseenOnly,
            string? sort,
            AdminService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            if (audience is not (null or "" or "all" or "subscribers" or "registered" or "guests"))
                return Results.BadRequest(new { error = "invalid_audience", message = "Неизвестный фильтр пользователей." });
            if (sort is not (null or "" or "newest" or "oldest"))
                return Results.BadRequest(new { error = "invalid_sort", message = "Неизвестный порядок сообщений." });
            var (actorId, _) = ctx.User.GetActor();
            var filter = audience switch
            {
                "subscribers" => AdminReadingAudience.Subscribers,
                "registered" => AdminReadingAudience.Registered,
                "guests" => AdminReadingAudience.Guests,
                _ => AdminReadingAudience.All
            };
            return Results.Ok(await service.SearchReadingsAsync(userId, search, page ?? 1, pageSize ?? 20, ct, includeInterpretation ?? true, filter,
                new AdminReadingReviewFilter(actorId, unseenOnly ?? false, sort == "oldest")));
        });

        group.MapPost("/readings/viewed", async (
            MarkReadingsViewedBody body, AdminService service, HttpContext ctx, CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            if (body?.ReadingIds is not { Length: > 0 and <= 100 })
                return Results.BadRequest(new { error = "invalid_reading_ids", message = "Передайте от 1 до 100 сообщений." });
            var (actorId, actorEmail) = ctx.User.GetActor();
            await service.MarkReadingsViewedAsync(actorId, actorEmail, body.ReadingIds, ct);
            return Results.NoContent();
        });

        group.MapGet("/readings/{id:guid}/interpretation", async (
            Guid id, AdminService service, HttpContext ctx, CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            return Results.Ok(await service.GetReadingInterpretationAsync(id, ct));
        });
    }

    private sealed record MarkReadingsViewedBody(Guid[]? ReadingIds);

    private static void MapStatsEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/stats", async (AdminService service, CancellationToken ct) =>
        {
            var stats = await service.GetStatsAsync(ct);
            return Results.Ok(stats);
        });
    }

    private static void MapUserEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/users", async (
            string? search,
            int? page,
            int? pageSize,
            AdminService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            var result = await service.SearchUsersAsync(search, page ?? 1, pageSize ?? 20, ct);
            return Results.Ok(new { items = result.Items, total = result.Total });
        });

        group.MapGet("/users/{id:guid}", async (
            Guid id,
            AdminService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            var dto = await service.GetUserDetailAsync(id, ct);
            return Results.Ok(dto);
        });

        group.MapPut("/users/{id:guid}/admin", async (
            Guid id,
            SetAdminBody body,
            AdminService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (body is null) return Results.BadRequest();
            var (actorId, actorEmail) = ctx.User.GetActor();
            var dto = await service.SetAdminAsync(actorId, actorEmail, id, body.IsAdmin, ct);
            return Results.Ok(dto);
        });

        group.MapDelete("/users/{id:guid}", async (
            Guid id,
            AdminService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var (actorId, actorEmail) = ctx.User.GetActor();
            await service.DeleteUserAsync(actorId, actorEmail, id, ct);
            return Results.NoContent();
        });

        group.MapPut("/users/{id:guid}/subscription", async (
            Guid id,
            SetSubscriptionBody body,
            AdminService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (body is null) return Results.BadRequest();
            var (actorId, actorEmail) = ctx.User.GetActor();
            var dto = await service.SetSubscriptionAsync(actorId, actorEmail, id, body.Status, body.ExpiresAt, ct);
            return Results.Ok(dto);
        });
    }

    private static void MapAchievementEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("/users/{id:guid}/achievements", async (
            Guid id,
            GrantAchievementBody body,
            AdminService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (body is null) return Results.BadRequest();
            var (actorId, actorEmail) = ctx.User.GetActor();
            var dto = await service.GrantAchievementAsync(actorId, actorEmail, id, body.Code, ct);
            return Results.Created($"/api/admin/users/{id}/achievements/{dto.Code}", dto);
        });

        group.MapDelete("/users/{id:guid}/achievements/{code}", async (
            Guid id,
            string code,
            AdminService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var (actorId, actorEmail) = ctx.User.GetActor();
            await service.RevokeAchievementAsync(actorId, actorEmail, id, code, ct);
            return Results.NoContent();
        });

        group.MapPost("/users/{id:guid}/achievements/recheck", async (
            Guid id,
            AdminService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var (actorId, actorEmail) = ctx.User.GetActor();
            var granted = await service.RecheckAchievementsAsync(actorId, actorEmail, id, ct);
            return Results.Ok(granted);
        });
    }

    private static (Guid Id, string Email) GetActor(this ClaimsPrincipal principal)
    {
        var id = principal.GetUserId() ?? throw new UnauthorizedException("Authentication required");
        var email = principal.FindFirstValue(ClaimTypes.Email)
                    ?? principal.FindFirstValue("email")
                    ?? "unknown";
        return (id, email);
    }

    public sealed class SetAdminBody
    {
        public bool IsAdmin { get; init; }
    }

    public sealed class SetSubscriptionBody
    {
        public SubscriptionStatus Status { get; init; }
        public DateTime? ExpiresAt { get; init; }
    }

    public sealed class GrantAchievementBody
    {
        public string Code { get; init; } = string.Empty;
    }

}
