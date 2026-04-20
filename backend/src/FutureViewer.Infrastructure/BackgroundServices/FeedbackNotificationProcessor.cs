using FutureViewer.DomainServices.Interfaces;
using FutureViewer.Infrastructure.Telegram;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FutureViewer.Infrastructure.BackgroundServices;

public sealed class FeedbackNotificationProcessor
{
    private readonly IFeedbackRepository _feedbacks;
    private readonly ITelegramNotifier _notifier;
    private readonly TelegramOptions _options;
    private readonly ILogger<FeedbackNotificationProcessor> _logger;

    public FeedbackNotificationProcessor(
        IFeedbackRepository feedbacks,
        ITelegramNotifier notifier,
        IOptions<TelegramOptions> options,
        ILogger<FeedbackNotificationProcessor> logger)
    {
        _feedbacks = feedbacks;
        _notifier = notifier;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<int> ProcessBatchAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var pending = await _feedbacks.GetPendingToNotifyAsync(now, _options.NotificationBatchSize, ct);
        if (pending.Count == 0) return 0;

        var processed = 0;
        foreach (var feedback in pending)
        {
            ct.ThrowIfCancellationRequested();

            var chatId = feedback.User?.TelegramChatId;
            if (chatId is null) continue;

            var question = feedback.Reading?.Question ?? string.Empty;
            var url = BuildFeedbackUrl(feedback.Token);

            var sent = await _notifier.SendFeedbackLinkAsync(chatId.Value, question, url, ct);
            if (!sent) continue;

            await _feedbacks.MarkNotifiedAsync(feedback.Id, DateTime.UtcNow, ct);
            processed++;
        }

        if (processed > 0)
            _logger.LogInformation("FeedbackNotificationProcessor sent {Count} feedback notifications", processed);

        return processed;
    }

    private string BuildFeedbackUrl(string token)
    {
        var baseUrl = string.IsNullOrWhiteSpace(_options.SiteUrl)
            ? "http://localhost:5173"
            : _options.SiteUrl.TrimEnd('/');
        return $"{baseUrl}/feedback/{Uri.EscapeDataString(token)}";
    }
}
