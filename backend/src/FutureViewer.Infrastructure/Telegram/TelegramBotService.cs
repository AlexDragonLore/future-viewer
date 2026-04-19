using FutureViewer.DomainServices.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace FutureViewer.Infrastructure.Telegram;

public sealed class TelegramBotService : ITelegramNotifier
{
    private readonly TelegramBotClientProvider _provider;
    private readonly TelegramOptions _options;
    private readonly ILogger<TelegramBotService> _logger;

    public TelegramBotService(
        TelegramBotClientProvider provider,
        IOptions<TelegramOptions> options,
        ILogger<TelegramBotService> logger)
    {
        _provider = provider;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsEnabled => _provider.IsEnabled && _options.IsEnabled;

    public async Task<bool> SendFeedbackLinkAsync(
        long chatId,
        string question,
        string feedbackUrl,
        CancellationToken ct = default)
    {
        var client = _provider.Client;
        if (client is null)
        {
            _logger.LogWarning("Telegram bot disabled; skipping feedback link to {ChatId}", chatId);
            return false;
        }

        var trimmedQuestion = string.IsNullOrWhiteSpace(question)
            ? "ваш вчерашний расклад"
            : question.Length > 200 ? question[..200] + "…" : question;

        var text =
            "Прошёл день с момента вашего расклада. Поделитесь: как развивались события? Следовали ли рекомендациям?\n\n" +
            $"Вопрос расклада: {trimmedQuestion}\n\n" +
            $"Открыть форму ответа: {feedbackUrl}";

        try
        {
            await client.SendMessage(
                chatId: chatId,
                text: text,
                parseMode: ParseMode.None,
                cancellationToken: ct);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send feedback link to {ChatId}", chatId);
            return false;
        }
    }

    public async Task<bool> SendAchievementNotificationAsync(
        long chatId,
        string name,
        string description,
        CancellationToken ct = default)
    {
        var client = _provider.Client;
        if (client is null)
        {
            _logger.LogWarning("Telegram bot disabled; skipping achievement to {ChatId}", chatId);
            return false;
        }

        var text = $"🏆 Новое достижение: {name}\n{description}";

        try
        {
            await client.SendMessage(
                chatId: chatId,
                text: text,
                parseMode: ParseMode.None,
                cancellationToken: ct);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send achievement notification to {ChatId}", chatId);
            return false;
        }
    }
}
