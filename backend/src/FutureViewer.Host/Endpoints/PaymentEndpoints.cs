using System.Security.Claims;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Exceptions;
using FutureViewer.DomainServices.Services;
using FutureViewer.Infrastructure.Payment;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;

namespace FutureViewer.Host.Endpoints;

public static class PaymentEndpoints
{
    private const long WebhookMaxBytes = 64 * 1024;

    public static IEndpointRouteBuilder MapPayments(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payments").WithTags("Payments");

        group.MapGet("/{publicOrderId:guid}/status", async (
            Guid publicOrderId, SubscriptionService service, HttpContext ctx, CancellationToken ct) =>
        {
            var userId = GetUserId(ctx.User)
                ?? throw new UnauthorizedException("Authentication required");
            return Results.Ok(await service.GetPaymentStatusAsync(userId, publicOrderId, ct));
        }).RequireAuthorization().RequireRateLimiting("privacy");

        group.MapPost("/subscribe", async (
            CreatePaymentRequest request,
            SubscriptionService service,
            HttpContext ctx,
            IOptions<PaymentOptions> paymentOptions,
            CancellationToken ct) =>
        {
            var userId = GetUserId(ctx.User)
                ?? throw new UnauthorizedException("Authentication required");
            if (!paymentOptions.Value.Enabled || !paymentOptions.Value.WebhookEnabled || !service.IsPaymentConfigured)
                throw new FeatureDisabledException(
                    "payments",
                    "Оплата временно недоступна. Дождитесь восстановления сервиса.");
            var payment = await service.CreatePaymentAsync(userId, request, ct);
            return Results.Ok(payment);
        }).RequireAuthorization().RequireRateLimiting("privacy");

        group.MapPost("/webhook", async (
            HttpContext ctx,
            SubscriptionService service,
            IOptions<PaymentOptions> paymentOptions,
            CancellationToken ct) =>
        {
            if (!paymentOptions.Value.Enabled || !paymentOptions.Value.WebhookEnabled)
                throw new FeatureDisabledException(
                    "payment_webhook",
                    "Приём платёжных уведомлений временно отключён оператором сервиса.");

            if (!service.IsWebhookSourceAllowed(GetClientAddress(ctx)))
                return Results.Unauthorized();

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

            var outcome = await service.ProcessWebhookWithOutcomeAsync(body, ct);
            return outcome == PaymentWebhookHandling.Rejected
                ? Results.BadRequest(new { error = "invalid_webhook" })
                : Results.Ok(new { handled = outcome == PaymentWebhookHandling.Processed });
        }).RequireRateLimiting("webhook");

        return app;
    }

    private static Guid? GetUserId(ClaimsPrincipal principal)
    {
        var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? principal.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    private static string? GetClientAddress(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString();
}
