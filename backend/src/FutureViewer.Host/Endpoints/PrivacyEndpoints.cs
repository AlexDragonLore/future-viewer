using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Exceptions;
using FutureViewer.DomainServices.Services;
using Microsoft.AspNetCore.Mvc;

namespace FutureViewer.Host.Endpoints;

public static class PrivacyEndpoints
{
    public static IEndpointRouteBuilder MapPrivacy(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/privacy")
            .WithTags("Privacy")
            .RequireAuthorization();

        group.MapGet("/settings", async (
            PrivacyService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = RequireUserId(ctx);
            return Results.Ok(await service.GetSettingsAsync(userId, ct));
        });

        group.MapPut("/settings/history", async (
            UpdateHistorySettingRequest request,
            PrivacyService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = RequireUserId(ctx);
            return Results.Ok(await service.UpdateHistoryAsync(
                userId, request.Enabled, ctx.TraceIdentifier, ct));
        });

        group.MapGet("/consents", async (
            PrivacyService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = RequireUserId(ctx);
            return Results.Ok(await service.GetConsentsAsync(userId, ct));
        });

        group.MapPost("/consents/{type}/revoke", async (
            string type,
            PrivacyService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = RequireUserId(ctx);
            await service.RevokeConsentAsync(userId, ParseConsentType(type), ctx.TraceIdentifier, ct);
            return Results.NoContent();
        });

        group.MapPost("/export", async (
            ReauthenticationRequest request,
            PrivacyService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = RequireUserId(ctx);
            return Results.Ok(await service.ExportAsync(
                userId, request.Password, ctx.TraceIdentifier, ct));
        }).RequireRateLimiting("privacy");

        group.MapDelete("/readings/{id:guid}", async (
            Guid id,
            [FromBody] ReauthenticationRequest request,
            PrivacyService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = RequireUserId(ctx);
            await service.DeleteReadingAsync(userId, id, request.Password, ctx.TraceIdentifier, ct);
            return Results.NoContent();
        }).RequireRateLimiting("privacy");

        group.MapDelete("/readings", async (
            [FromBody] ReauthenticationRequest request,
            PrivacyService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = RequireUserId(ctx);
            await service.DeleteAllReadingsAsync(userId, request.Password, ctx.TraceIdentifier, ct);
            return Results.NoContent();
        }).RequireRateLimiting("privacy");

        group.MapPost("/account-deletion", async (
            ReauthenticationRequest request,
            PrivacyService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = RequireUserId(ctx);
            var status = await service.RequestAccountDeletionAsync(
                userId, request.Password, ctx.TraceIdentifier, ct);
            return Results.Accepted(value: status);
        }).RequireRateLimiting("privacy");

        group.MapGet("/account-deletion/status", async (
            PrivacyService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = RequireUserId(ctx);
            return Results.Ok(await service.GetAccountDeletionStatusAsync(userId, ct));
        });

        return app;
    }

    private static Guid RequireUserId(HttpContext ctx) =>
        ctx.User.GetUserId() ?? throw new UnauthorizedException("Authentication required");

    private static ConsentType ParseConsentType(string value) => value.Trim().ToLowerInvariant() switch
    {
        "personalization" => ConsentType.Personalization,
        "marketing" => ConsentType.Marketing,
        "analytics" => ConsentType.Analytics,
        _ => throw new DomainException("Unknown optional consent type.")
    };
}
