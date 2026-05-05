namespace FutureViewer.Infrastructure.Telegram;

public sealed class TelegramOptions
{
    public const string SectionName = "Telegram";

    public string BotToken { get; set; } = string.Empty;
    public string BotUsername { get; set; } = "FutureViewerBot";
    public string SiteUrl { get; set; } = "http://localhost:5173";
    public int NotificationBatchSize { get; set; } = 50;
    public int NotificationPollIntervalSeconds { get; set; } = 300;

    public bool IsEnabled => !string.IsNullOrWhiteSpace(BotToken);
}
