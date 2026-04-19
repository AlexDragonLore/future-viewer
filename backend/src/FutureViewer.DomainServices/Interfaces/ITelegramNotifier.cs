namespace FutureViewer.DomainServices.Interfaces;

public interface ITelegramNotifier
{
    Task<bool> SendFeedbackLinkAsync(long chatId, string question, string feedbackUrl, CancellationToken ct = default);
    Task<bool> SendAchievementNotificationAsync(long chatId, string name, string description, CancellationToken ct = default);
}
