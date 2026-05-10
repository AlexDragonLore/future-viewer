using System.Security.Claims;
using FluentValidation;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Exceptions;
using FutureViewer.DomainServices.Services;

namespace FutureViewer.Host.Endpoints;

public static class ProfileEndpoints
{
    public static IEndpointRouteBuilder MapProfile(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/profile")
            .WithTags("Profile")
            .RequireAuthorization();

        group.MapGet("/personalization", async (
            PersonalizationService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = GetUserId(ctx.User)
                ?? throw new UnauthorizedException("Authentication required");
            return Results.Ok(await service.GetAsync(userId, ct));
        });

        group.MapPut("/personalization", async (
            UpdatePersonalizationRequest request,
            IValidator<UpdatePersonalizationRequest> validator,
            PersonalizationService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            var userId = GetUserId(ctx.User)
                ?? throw new UnauthorizedException("Authentication required");
            return Results.Ok(await service.UpdateAsync(userId, request, ct));
        });

        group.MapDelete("/personalization/memory/{id:guid}", async (
            Guid id,
            PersonalizationService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = GetUserId(ctx.User)
                ?? throw new UnauthorizedException("Authentication required");
            var deleted = await service.DeleteMemoryRuleAsync(userId, id, ct);
            return deleted ? Results.NoContent() : Results.NotFound(new { error = "not_found", message = "Memory rule not found" });
        });

        group.MapDelete("/personalization/memory", async (
            PersonalizationService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = GetUserId(ctx.User)
                ?? throw new UnauthorizedException("Authentication required");
            await service.DeleteAllMemoryAsync(userId, ct);
            return Results.NoContent();
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
