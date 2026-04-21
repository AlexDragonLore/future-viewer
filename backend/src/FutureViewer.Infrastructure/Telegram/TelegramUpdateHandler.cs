using FutureViewer.DomainServices.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace FutureViewer.Infrastructure.Telegram;

public sealed class TelegramUpdateHandler : IUpdateHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TelegramUpdateHandler> _logger;

    public TelegramUpdateHandler(
        IServiceScopeFactory scopeFactory,
        ILogger<TelegramUpdateHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task HandleUpdateAsync(
        ITelegramBotClient botClient,
        Update update,
        CancellationToken cancellationToken)
    {
        if (update.Type != UpdateType.Message || update.Message is null) return;
        var message = update.Message;
        if (message.Type != MessageType.Text || string.IsNullOrEmpty(message.Text)) return;

        var text = message.Text.Trim();
        if (!text.StartsWith("/start", StringComparison.OrdinalIgnoreCase)) return;

        var parts = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var chatId = message.Chat.Id;

        if (parts.Length < 2)
        {
            await botClient.SendMessage(
                chatId: chatId,
                text: "Привет! Чтобы привязать аккаунт, откройте приложение «Вуаль Грядущего» и нажмите «Привязать Telegram».",
                cancellationToken: cancellationToken);
            return;
        }

        var token = parts[1].Trim();

        using var scope = _scopeFactory.CreateScope();
        var linkService = scope.ServiceProvider.GetRequiredService<TelegramLinkService>();
        var linked = await linkService.CompleteLinkAsync(token, chatId, cancellationToken);

        var reply = linked
            ? "Аккаунт успешно привязан! Теперь вы будете получать уведомления о ваших раскладах."
            : "Не удалось привязать аккаунт: ссылка недействительна или уже использована.";

        await botClient.SendMessage(
            chatId: chatId,
            text: reply,
            cancellationToken: cancellationToken);
    }

    public Task HandleErrorAsync(
        ITelegramBotClient botClient,
        Exception exception,
        HandleErrorSource source,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning(exception, "Telegram update handler error from {Source}", source);
        return Task.CompletedTask;
    }
}
