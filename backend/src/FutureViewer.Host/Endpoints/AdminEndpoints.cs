using System.Security.Claims;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.Exceptions;
using FutureViewer.DomainServices.Services;
using FutureViewer.Infrastructure.BackgroundServices;

namespace FutureViewer.Host.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdmin(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin")
            .WithTags("Admin")
            .RequireAuthorization("Admin");

        MapFeedbackEndpoints(group);
        MapUserEndpoints(group);

        return app;
    }

    private static void MapFeedbackEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/feedbacks", async (
            Guid? userId,
            FeedbackStatus? status,
            int? page,
            int? pageSize,
            AdminService service,
            CancellationToken ct) =>
        {
            var result = await service.SearchFeedbacksAsync(userId, status, page ?? 1, pageSize ?? 20, ct);
            return Results.Ok(new { items = result.Items, total = result.Total });
        });

        group.MapPost("/feedbacks", async (
            CreateAdminFeedbackBody body,
            AdminService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (body is null) return Results.BadRequest();
            var (actorId, actorEmail) = ctx.User.GetActor();
            var dto = await service.CreateScheduledAsync(
                actorId, actorEmail,
                body.ReadingId, body.ScheduledAt, body.BypassDelay, body.Replace, ct);
            return Results.Created($"/api/admin/feedbacks/{dto.Id}", dto);
        });

        group.MapPost("/feedbacks/synthetic", async (
            CreateSyntheticFeedbackBody body,
            AdminService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (body is null) return Results.BadRequest();
            var (actorId, actorEmail) = ctx.User.GetActor();
            var dto = await service.CreateSyntheticAsync(
                actorId, actorEmail,
                body.ReadingId, body.AiScore, body.AiScoreReason,
                body.IsSincere ?? true, body.SelfReport, ct);
            return Results.Created($"/api/admin/feedbacks/{dto.Id}", dto);
        });

        group.MapPut("/feedbacks/{id:guid}", async (
            Guid id,
            UpdateAdminFeedbackBody body,
            AdminService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (body is null) return Results.BadRequest();
            var (actorId, actorEmail) = ctx.User.GetActor();
            var dto = await service.UpdateAsync(actorId, actorEmail, id, new AdminFeedbackUpdate
            {
                AiScore = body.AiScore,
                AiScoreReason = body.AiScoreReason,
                IsSincere = body.IsSincere,
                Status = body.Status,
                SelfReport = body.SelfReport,
                ScheduledAt = body.ScheduledAt,
                NotifiedAt = body.NotifiedAt,
                AnsweredAt = body.AnsweredAt
            }, ct);
            return Results.Ok(dto);
        });

        group.MapDelete("/feedbacks/{id:guid}", async (
            Guid id,
            AdminService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var (actorId, actorEmail) = ctx.User.GetActor();
            await service.DeleteFeedbackAsync(actorId, actorEmail, id, ct);
            return Results.NoContent();
        });

        group.MapPost("/feedbacks/run-notifications", async (
            FeedbackNotificationProcessor processor,
            CancellationToken ct) =>
        {
            var processed = await processor.ProcessBatchAsync(ct);
            return Results.Ok(new { processed });
        });
    }

    private static void MapUserEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/users", async (
            string? search,
            int? page,
            int? pageSize,
            AdminService service,
            CancellationToken ct) =>
        {
            var result = await service.SearchUsersAsync(search, page ?? 1, pageSize ?? 20, ct);
            return Results.Ok(new { items = result.Items, total = result.Total });
        });

        group.MapGet("/users/{id:guid}", async (
            Guid id,
            AdminService service,
            CancellationToken ct) =>
        {
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

    private static (Guid Id, string Email) GetActor(this ClaimsPrincipal principal)
    {
        var id = principal.GetUserId() ?? throw new UnauthorizedException("Authentication required");
        var email = principal.FindFirstValue(ClaimTypes.Email)
                    ?? principal.FindFirstValue("email")
                    ?? "unknown";
        return (id, email);
    }

    public sealed class CreateAdminFeedbackBody
    {
        public Guid ReadingId { get; init; }
        public DateTime? ScheduledAt { get; init; }
        public bool BypassDelay { get; init; }
        public bool Replace { get; init; }
    }

    public sealed class CreateSyntheticFeedbackBody
    {
        public Guid ReadingId { get; init; }
        public int AiScore { get; init; }
        public string? AiScoreReason { get; init; }
        public bool? IsSincere { get; init; }
        public string? SelfReport { get; init; }
    }

    public sealed class UpdateAdminFeedbackBody
    {
        public int? AiScore { get; init; }
        public string? AiScoreReason { get; init; }
        public bool? IsSincere { get; init; }
        public FeedbackStatus? Status { get; init; }
        public string? SelfReport { get; init; }
        public DateTime? ScheduledAt { get; init; }
        public DateTime? NotifiedAt { get; init; }
        public DateTime? AnsweredAt { get; init; }
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
}
