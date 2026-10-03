using System.Security.Claims;
using System.Text.Json;
using FluentValidation;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Interfaces;
using FutureViewer.DomainServices.Services;
using FutureViewer.Host.Auth;

namespace FutureViewer.Host.Endpoints;

public static class ReadingEndpoints
{
    private static readonly JsonSerializerOptions StreamJsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapReadings(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/readings").WithTags("Readings");

        group.MapPost("/guest", async (
            CreateReadingRequest request,
            IValidator<CreateReadingRequest> validator,
            ReadingService service,
            GuestReadingTickets tickets,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            await validator.ValidateAndThrowAsync(request, ct);
            var reading = await service.CreateGuestAsync(request, ct);
            return Results.Ok(tickets.Issue(reading));
        }).AllowAnonymous().RequireRateLimiting("guest-reading");

        group.MapPost("/guest/preview", (
            GuestReadingTicketRequest request, GuestReadingTickets tickets, HttpContext ctx) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            return Results.Ok(GuestReadingTickets.Preview(tickets.Read(request.Ticket)));
        }).AllowAnonymous().RequireRateLimiting("ai");

        group.MapPost("/guest/unlock", async (
            GuestReadingTicketRequest request,
            GuestReadingTickets tickets,
            ReadingService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "no-store";
            var userId = GetUserId(ctx.User)
                ?? throw new DomainServices.Exceptions.UnauthorizedException("Authentication required");
            return Results.Ok(await service.UnlockGuestAsync(tickets.Read(request.Ticket), userId, ct));
        }).RequireAuthorization().RequireRateLimiting("ai");

        group.MapPost("/", async (
            CreateReadingRequest request,
            IValidator<CreateReadingRequest> validator,
            ReadingService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            var userId = GetUserId(ctx.User)
                ?? throw new DomainServices.Exceptions.UnauthorizedException("Authentication required");
            var result = await service.CreateAsync(request, userId, ct);
            return Results.Created($"/api/readings/{result.Id}", result);
        }).RequireAuthorization().RequireRateLimiting("ai");

        group.MapPost("/stream", async (
            CreateReadingRequest request,
            IValidator<CreateReadingRequest> validator,
            ReadingService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            var userId = GetUserId(ctx.User)
                ?? throw new DomainServices.Exceptions.UnauthorizedException("Authentication required");

            ctx.Response.Headers.ContentType = "application/x-ndjson";
            ctx.Response.Headers.CacheControl = "no-cache";
            ctx.Response.Headers["X-Accel-Buffering"] = "no";

            var newline = "\n"u8.ToArray();
            var anyChunkWritten = false;

            try
            {
                await foreach (var evt in service.CreateStreamAsync(request, userId, ct))
                {
                    object payload = evt switch
                    {
                        ReadingStreamEvent.Cards c => new { type = "cards", reading = c.Reading },
                        ReadingStreamEvent.Chunk ch => new { type = "chunk", delta = ch.Delta },
                        ReadingStreamEvent.Done => new { type = "done" },
                        _ => throw new InvalidOperationException("Unknown stream event")
                    };
                    await JsonSerializer.SerializeAsync(ctx.Response.Body, payload, StreamJsonOptions, ct);
                    await ctx.Response.Body.WriteAsync(newline, ct);
                    await ctx.Response.Body.FlushAsync(ct);
                    anyChunkWritten = true;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Client disconnected; nothing to send.
                throw;
            }
            catch (Exception) when (anyChunkWritten && ctx.Response.HasStarted)
            {
                // Headers already flushed, so ExceptionHandlerMiddleware can't respond.
                // Emit a terminal error frame so the client can surface the failure instead of hanging.
                try
                {
                    var payload = new { type = "error", message = "Не удалось завершить интерпретацию" };
                    await JsonSerializer.SerializeAsync(ctx.Response.Body, payload, StreamJsonOptions, CancellationToken.None);
                    await ctx.Response.Body.WriteAsync(newline, CancellationToken.None);
                    await ctx.Response.Body.FlushAsync(CancellationToken.None);
                }
                catch
                {
                    // Best-effort terminal frame.
                }
            }
        }).RequireAuthorization().RequireRateLimiting("ai");

        group.MapPost("/validate-question", async (
            CreateReadingRequest request,
            IValidator<CreateReadingRequest> validator,
            IAIQuestionValidator questionValidator,
            SubscriptionService subscription,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            var userId = GetUserId(ctx.User)
                ?? throw new DomainServices.Exceptions.UnauthorizedException("Authentication required");

            var validation = await questionValidator.ValidateAsync(request.Question, ct);
            var hasActiveSubscription = await subscription.HasActiveSubscriptionAsync(userId, ct);
            var result = QuestionValidationPolicy.BuildCheck(request.Question, validation, hasActiveSubscription);
            return Results.Ok(result);
        }).RequireAuthorization().RequireRateLimiting("ai");

        group.MapGet("/{id:guid}", async (
            Guid id,
            ReadingService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = GetUserId(ctx.User)
                ?? throw new DomainServices.Exceptions.UnauthorizedException("Authentication required");
            var result = await service.GetAsync(id, userId, ct);
            return Results.Ok(result);
        }).RequireAuthorization();

        group.MapGet("/history", async (
            ReadingService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = GetUserId(ctx.User)
                ?? throw new DomainServices.Exceptions.UnauthorizedException("Authentication required");
            var history = await service.GetHistoryAsync(userId, ct);
            return Results.Ok(history);
        }).RequireAuthorization();

        group.MapDelete("/{id:guid}", async (
            Guid id,
            ReadingService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = GetUserId(ctx.User)
                ?? throw new DomainServices.Exceptions.UnauthorizedException("Authentication required");
            await service.DeleteFromHistoryAsync(id, userId, ct);
            return Results.NoContent();
        }).RequireAuthorization();

        return app;
    }

    private static Guid? GetUserId(ClaimsPrincipal principal)
    {
        var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? principal.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
