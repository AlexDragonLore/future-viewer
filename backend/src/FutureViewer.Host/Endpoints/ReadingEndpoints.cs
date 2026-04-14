using System.Security.Claims;
using FluentValidation;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Services;

namespace FutureViewer.Host.Endpoints;

public static class ReadingEndpoints
{
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
