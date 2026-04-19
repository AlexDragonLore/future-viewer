using Microsoft.Extensions.Options;
using Telegram.Bot;

namespace FutureViewer.Infrastructure.Telegram;

public sealed class TelegramBotClientProvider
{
    public TelegramBotClientProvider(IOptions<TelegramOptions> options)
    {
        var opts = options.Value;
        Client = opts.IsEnabled ? new TelegramBotClient(opts.BotToken) : null;
    }

    public ITelegramBotClient? Client { get; }
    public bool IsEnabled => Client is not null;
}
