namespace FutureViewer.Infrastructure.AI;

public sealed class AIOptions
{
    public const string SectionName = "AI";
    public string Provider { get; set; } = "OpenAI";
    public bool Enabled { get; set; } = true;
    public string RegistryPath { get; set; } = "config/processors.json";
}
