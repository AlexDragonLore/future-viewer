using System.Security.Claims;
using FutureViewer.DomainServices.Exceptions;
using FutureViewer.DomainServices.Services;

namespace FutureViewer.Host.Endpoints;

public static class PaymentEndpoints
{
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
            HttpRequest request,
            SubscriptionService service,
            CancellationToken ct) =>
        {
            using var reader = new StreamReader(request.Body);
            var body = await reader.ReadToEndAsync(ct);
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
