using System.Security.Claims;
using System.Text.Json;
using FluentValidation;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Services;

namespace FutureViewer.Host.Endpoints;

public static class ReadingEndpoints
{
    private static readonly JsonSerializerOptions StreamJsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapReadings(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/readings").WithTags("Readings");

        group.MapPost("/", async (
            CreateReadingRequest request,
            IValidator<CreateReadingRequest> validator,
            ReadingService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            var userId = GetUserId(ctx.User);
            var result = await service.CreateAsync(request, userId, ct);
            return Results.Created($"/api/readings/{result.Id}", result);
        });

        group.MapPost("/stream", async (
            CreateReadingRequest request,
            IValidator<CreateReadingRequest> validator,
            ReadingService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            var userId = GetUserId(ctx.User);

            ctx.Response.Headers.ContentType = "application/x-ndjson";
            ctx.Response.Headers.CacheControl = "no-cache";
            ctx.Response.Headers["X-Accel-Buffering"] = "no";

            var newline = "\n"u8.ToArray();

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
            }
        });

        group.MapGet("/{id:guid}", async (Guid id, ReadingService service, CancellationToken ct) =>
        {
            var result = await service.GetAsync(id, ct);
            return Results.Ok(result);
        });

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

        return app;
    }

    private static Guid? GetUserId(ClaimsPrincipal principal)
    {
        var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? principal.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
