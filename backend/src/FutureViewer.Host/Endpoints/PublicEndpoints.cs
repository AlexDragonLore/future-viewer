using Microsoft.Extensions.Options;
using FutureViewer.Infrastructure.Payment;
using FutureViewer.DomainServices.Interfaces;

namespace FutureViewer.Host.Endpoints;

public static class PublicEndpoints
{
    public static IEndpointRouteBuilder MapPublic(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/public").WithTags("Public");

        group.MapGet("/config", (IOptions<SupportOptions> support, IOptions<PaymentOptions> payment, IPaymentProvider provider) =>
            Results.Ok(new
            {
                supportEmail = support.Value.Email,
                paymentsEnabled = payment.Value.Enabled && payment.Value.WebhookEnabled && provider.IsConfigured,
                paymentProduct = provider.IsConfigured ? provider.Product : null,
            }));

        return app;
    }
}
