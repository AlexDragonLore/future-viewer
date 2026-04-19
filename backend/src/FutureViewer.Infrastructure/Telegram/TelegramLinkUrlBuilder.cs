using FutureViewer.DomainServices.Interfaces;
using Microsoft.Extensions.Options;

namespace FutureViewer.Infrastructure.Telegram;

public sealed class TelegramLinkUrlBuilder : ITelegramLinkUrlBuilder
{
    private readonly TelegramOptions _options;

    public TelegramLinkUrlBuilder(IOptions<TelegramOptions> options)
    {
        _options = options.Value;
    }

    public string BuildDeepLink(string token)
    {
        var username = string.IsNullOrWhiteSpace(_options.BotUsername)
            ? "FutureViewerBot"
            : _options.BotUsername;
        return $"https://t.me/{username}?start={Uri.EscapeDataString(token)}";
    }
}
