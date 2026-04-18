using System.Security.Claims;
using FutureViewer.DomainServices.Exceptions;
using FutureViewer.DomainServices.Services;
using Microsoft.AspNetCore.Http.Features;

namespace FutureViewer.Host.Endpoints;

public static class PaymentEndpoints
{
    private const long WebhookMaxBytes = 64 * 1024;

    public static IEndpointRouteBuilder MapPayments(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payments").WithTags("Payments");

        group.MapPost("/subscribe", async (
            SubscriptionService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = GetUserId(ctx.User)
                ?? throw new UnauthorizedException("Authentication required");
            var payment = await service.CreatePaymentAsync(userId, ct);
            return Results.Ok(payment);
        }).RequireAuthorization();

        group.MapPost("/webhook", async (
            HttpContext ctx,
            SubscriptionService service,
            CancellationToken ct) =>
        {
            if (ctx.Request.ContentLength is long cl && cl > WebhookMaxBytes)
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

            var sizeFeature = ctx.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (sizeFeature is { IsReadOnly: false })
                sizeFeature.MaxRequestBodySize = WebhookMaxBytes;

            string body;
            try
            {
                using var reader = new StreamReader(ctx.Request.Body);
                body = await reader.ReadToEndAsync(ct);
            }
            catch (BadHttpRequestException)
            {
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            var handled = await service.ProcessWebhookAsync(body, ct);
            return handled ? Results.Ok() : Results.Ok(new { handled = false });
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
