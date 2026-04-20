using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;

namespace FutureViewer.Infrastructure.Telegram;

public sealed class TelegramPollingHostedService : BackgroundService
{
    private readonly TelegramBotClientProvider _provider;
    private readonly IUpdateHandler _handler;
    private readonly TelegramOptions _options;
    private readonly ILogger<TelegramPollingHostedService> _logger;

    public TelegramPollingHostedService(
        TelegramBotClientProvider provider,
        IUpdateHandler handler,
        IOptions<TelegramOptions> options,
        ILogger<TelegramPollingHostedService> logger)
    {
        _provider = provider;
        _handler = handler;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var client = _provider.Client;
        if (client is null || !_options.IsEnabled)
        {
            _logger.LogInformation("Telegram disabled; polling service not starting");
            return;
        }

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>(),
            DropPendingUpdates = true
        };

        try
        {
            await client.DeleteWebhook(dropPendingUpdates: true, cancellationToken: stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear Telegram webhook before starting polling");
        }

        _logger.LogInformation("Starting Telegram long polling");

        try
        {
            await client.ReceiveAsync(_handler, receiverOptions, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Telegram polling loop failed");
        }
    }
}
