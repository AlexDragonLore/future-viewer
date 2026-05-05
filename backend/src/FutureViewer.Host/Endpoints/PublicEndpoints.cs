using Microsoft.Extensions.Options;

namespace FutureViewer.Host.Endpoints;

public static class PublicEndpoints
{
    public static IEndpointRouteBuilder MapPublic(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/public").WithTags("Public");

        group.MapGet("/config", (IOptions<SupportOptions> support) =>
            Results.Ok(new { supportEmail = support.Value.Email }));

        return app;
    }
}
