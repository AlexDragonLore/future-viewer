namespace FutureViewer.Infrastructure.AI;

public sealed class AIOptions
{
    public const string SectionName = "AI";
    public string Provider { get; set; } = "OpenAI";
}
