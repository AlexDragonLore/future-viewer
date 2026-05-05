namespace FutureViewer.Infrastructure.AI;

public sealed class DeepSeekOptions
{
    public const string SectionName = "DeepSeek";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "deepseek-v4-flash";
    public string BaseUrl { get; set; } = "https://api.deepseek.com";
}
