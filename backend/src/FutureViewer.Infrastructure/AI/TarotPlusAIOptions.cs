namespace FutureViewer.Infrastructure.AI;

public sealed class TarotPlusAIOptions
{
    public const string SectionName = "TarotPlusAI";

    public string Provider { get; set; } = "DeepSeek";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "deepseek-v4-pro";
    public string BaseUrl { get; set; } = "https://api.deepseek.com";
    public int RequestTimeoutSeconds { get; set; } = 180;
    public int FastRequestTimeoutSeconds { get; set; } = 10;
    public int FollowUpRequestTimeoutSeconds { get; set; } = 45;
    public int ReportRequestTimeoutSeconds { get; set; } = 30;
}
