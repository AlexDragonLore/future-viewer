using FutureViewer.Infrastructure.Telegram;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FutureViewer.Infrastructure.BackgroundServices;

public sealed class FeedbackNotificationJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TelegramOptions _options;
    private readonly ILogger<FeedbackNotificationJob> _logger;

    public FeedbackNotificationJob(
        IServiceScopeFactory scopeFactory,
        IOptions<TelegramOptions> options,
        ILogger<FeedbackNotificationJob> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.IsEnabled)
        {
            _logger.LogInformation("Telegram disabled; FeedbackNotificationJob not running");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(60, _options.NotificationPollIntervalSeconds));
        _logger.LogInformation("FeedbackNotificationJob started with interval {Interval}", interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<FeedbackNotificationProcessor>();
                await processor.ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FeedbackNotificationJob iteration failed");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
