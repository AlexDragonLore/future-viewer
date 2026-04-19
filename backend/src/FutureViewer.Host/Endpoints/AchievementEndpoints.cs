using FutureViewer.DomainServices.Exceptions;
using FutureViewer.DomainServices.Interfaces;
using FutureViewer.DomainServices.Services;

namespace FutureViewer.Host.Endpoints;

public static class AchievementEndpoints
{
    public static IEndpointRouteBuilder MapAchievements(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/achievements").WithTags("Achievements");

        group.MapGet("/", async (
            IAchievementRepository repo,
            CancellationToken ct) =>
        {
            var all = await repo.GetAllAsync(ct);
            var dtos = all
                .OrderBy(a => a.SortOrder)
                .Select(a => new
                {
                    id = a.Id,
                    code = a.Code,
                    name = a.NameRu,
                    description = a.DescriptionRu,
                    iconPath = a.IconPath,
                    sortOrder = a.SortOrder
                });
            return Results.Ok(dtos);
        });

        group.MapGet("/me", async (
            AchievementService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = ctx.User.GetUserId()
                ?? throw new UnauthorizedException("Authentication required");
            await service.CheckAndGrantAsync(userId, ct);
            var list = await service.GetAllWithUserStatusAsync(userId, ct);
            return Results.Ok(list);
        }).RequireAuthorization();

        return app;
    }
}
