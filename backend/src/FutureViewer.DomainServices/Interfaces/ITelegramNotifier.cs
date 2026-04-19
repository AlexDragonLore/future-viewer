namespace FutureViewer.DomainServices.Interfaces;

public interface ITelegramNotifier
{
    Task SendFeedbackLinkAsync(long chatId, string question, string feedbackUrl, CancellationToken ct = default);
    Task SendAchievementNotificationAsync(long chatId, string name, string description, CancellationToken ct = default);
}
