using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Exceptions;
using FutureViewer.DomainServices.Services;

namespace FutureViewer.Host.Endpoints;

public static class TarotPlusEndpoints
{
    public static IEndpointRouteBuilder MapTarotPlus(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tarot-plus").WithTags("Tarot+").RequireAuthorization();

        group.MapPost("/preview", async (
            CreateTarotPlusPreviewRequest request,
            TarotPlusService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = ctx.User.GetUserId()
                ?? throw new UnauthorizedException("Authentication required");
            var result = await service.CreatePreviewAsync(request, userId, ct);
            return Results.Created($"/api/tarot-plus/{result.Session.Id}", result);
        });

        group.MapPost("/{id:guid}/payment", async (
            Guid id,
            TarotPlusService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = ctx.User.GetUserId()
                ?? throw new UnauthorizedException("Authentication required");
            var result = await service.CreatePaymentAsync(id, userId, ct);
            return Results.Ok(result);
        });

        group.MapGet("/{id:guid}", async (
            Guid id,
            TarotPlusService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = ctx.User.GetUserId()
                ?? throw new UnauthorizedException("Authentication required");
            return Results.Ok(await service.GetAsync(id, userId, ct));
        });

        group.MapGet("/history", async (
            TarotPlusService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = ctx.User.GetUserId()
                ?? throw new UnauthorizedException("Authentication required");
            return Results.Ok(await service.GetHistoryAsync(userId, ct));
        });

        group.MapPost("/{id:guid}/answers", async (
            Guid id,
            AddTarotPlusAnswerRequest request,
            TarotPlusService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = ctx.User.GetUserId()
                ?? throw new UnauthorizedException("Authentication required");
            return Results.Ok(await service.AddAnswerAsync(id, userId, request, ct));
        });

        group.MapGet("/{id:guid}/next-step", async (
            Guid id,
            TarotPlusService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = ctx.User.GetUserId()
                ?? throw new UnauthorizedException("Authentication required");
            return Results.Ok(await service.GetNextStepAsync(id, userId, ct));
        });

        group.MapPost("/{id:guid}/generate-report", async (
            Guid id,
            TarotPlusService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = ctx.User.GetUserId()
                ?? throw new UnauthorizedException("Authentication required");
            return Results.Ok(await service.GenerateReportAsync(id, userId, ct));
        });

        group.MapPost("/{id:guid}/follow-up", async (
            Guid id,
            TarotPlusFollowUpRequest request,
            TarotPlusService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = ctx.User.GetUserId()
                ?? throw new UnauthorizedException("Authentication required");
            return Results.Ok(await service.AskFollowUpAsync(id, userId, request, ct));
        });

        return app;
    }
}
