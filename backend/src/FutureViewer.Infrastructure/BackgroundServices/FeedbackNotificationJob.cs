using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.Interfaces;
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
                await ProcessBatchAsync(stoppingToken);
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

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var feedbacks = scope.ServiceProvider.GetRequiredService<IFeedbackRepository>();
        var notifier = scope.ServiceProvider.GetRequiredService<ITelegramNotifier>();

        var now = DateTime.UtcNow;
        var pending = await feedbacks.GetPendingToNotifyAsync(now, _options.NotificationBatchSize, ct);
        if (pending.Count == 0) return;

        foreach (var feedback in pending)
        {
            ct.ThrowIfCancellationRequested();

            var chatId = feedback.User?.TelegramChatId;
            if (chatId is null) continue;

            var question = feedback.Reading?.Question ?? string.Empty;
            var url = BuildFeedbackUrl(feedback.Token);

            var sent = await notifier.SendFeedbackLinkAsync(chatId.Value, question, url, ct);
            if (!sent) continue;

            feedback.Status = FeedbackStatus.Notified;
            feedback.NotifiedAt = DateTime.UtcNow;
            await feedbacks.UpdateAsync(feedback, ct);
        }
    }

    private string BuildFeedbackUrl(string token)
    {
        var baseUrl = string.IsNullOrWhiteSpace(_options.SiteUrl)
            ? "http://localhost:5173"
            : _options.SiteUrl.TrimEnd('/');
        return $"{baseUrl}/feedback/{Uri.EscapeDataString(token)}";
    }
}
