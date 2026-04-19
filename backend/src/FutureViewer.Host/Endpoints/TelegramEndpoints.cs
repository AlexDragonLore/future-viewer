using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FutureViewer.DomainServices.Exceptions;
using FutureViewer.DomainServices.Services;
using FutureViewer.Infrastructure.Telegram;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;

namespace FutureViewer.Host.Endpoints;

public static class TelegramEndpoints
{
    private const string SecretTokenHeader = "X-Telegram-Bot-Api-Secret-Token";
    private const long WebhookMaxBytes = 64 * 1024;

    private static readonly JsonSerializerOptions UpdateJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static IEndpointRouteBuilder MapTelegram(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/telegram").WithTags("Telegram");

        group.MapPost("/link", async (
            TelegramLinkService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = ctx.User.GetUserId()
                ?? throw new UnauthorizedException("Authentication required");
            var response = await service.GenerateLinkAsync(userId, ct);
            return Results.Ok(response);
        }).RequireAuthorization();

        group.MapDelete("/link", async (
            TelegramLinkService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = ctx.User.GetUserId()
                ?? throw new UnauthorizedException("Authentication required");
            await service.UnlinkAsync(userId, ct);
            return Results.NoContent();
        }).RequireAuthorization();

        group.MapGet("/status", async (
            TelegramLinkService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = ctx.User.GetUserId()
                ?? throw new UnauthorizedException("Authentication required");
            var isLinked = await service.IsLinkedAsync(userId, ct);
            return Results.Ok(new { isLinked });
        }).RequireAuthorization();

        group.MapPost("/webhook", async (
            HttpContext ctx,
            IOptions<TelegramOptions> options,
            TelegramBotClientProvider provider,
            IUpdateHandler handler,
            CancellationToken ct) =>
        {
            var opts = options.Value;

            if (string.IsNullOrEmpty(opts.SecretToken))
                return Results.StatusCode(StatusCodes.Status401Unauthorized);

            var header = ctx.Request.Headers[SecretTokenHeader].ToString();
            var headerBytes = Encoding.UTF8.GetBytes(header);
            var secretBytes = Encoding.UTF8.GetBytes(opts.SecretToken);
            if (!CryptographicOperations.FixedTimeEquals(headerBytes, secretBytes))
                return Results.StatusCode(StatusCodes.Status401Unauthorized);

            if (ctx.Request.ContentLength is long cl && cl > WebhookMaxBytes)
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

            var client = provider.Client;
            if (client is null)
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

            Update? update;
            try
            {
                update = await JsonSerializer.DeserializeAsync<Update>(
                    ctx.Request.Body, UpdateJsonOptions, ct);
            }
            catch (JsonException)
            {
                return Results.BadRequest();
            }

            if (update is null) return Results.Ok();

            await handler.HandleUpdateAsync(client, update, ct);
            return Results.Ok();
        });

        return app;
    }
}
