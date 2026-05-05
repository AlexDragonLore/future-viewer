using System.ClientModel;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;

namespace FutureViewer.Infrastructure.AI;

public sealed class AIChatClientFactory
{
    private readonly OpenAIClient _client;

    public AIChatClientFactory(
        IOptions<AIOptions> aiOptions,
        IOptions<OpenAIOptions> openAIOptions,
        IOptions<DeepSeekOptions> deepSeekOptions)
    {
        var settings = ResolveSettings(aiOptions.Value, openAIOptions.Value, deepSeekOptions.Value);
        Provider = settings.Provider;
        Model = settings.Model;

        if (string.IsNullOrWhiteSpace(settings.ApiKey))
            throw new InvalidOperationException($"{settings.ConfigurationSection}:ApiKey is not configured");

        if (string.IsNullOrWhiteSpace(settings.Model))
            throw new InvalidOperationException($"{settings.ConfigurationSection}:Model is not configured");

        var clientOptions = new OpenAIClientOptions();
        if (!string.IsNullOrWhiteSpace(settings.BaseUrl))
            clientOptions.Endpoint = new Uri(settings.BaseUrl, UriKind.Absolute);

        _client = new OpenAIClient(new ApiKeyCredential(settings.ApiKey), clientOptions);
    }

    public string Provider { get; }
    public string Model { get; }

    public ChatClient CreateChatClient() => _client.GetChatClient(Model);

    private static AIClientSettings ResolveSettings(
        AIOptions aiOptions,
        OpenAIOptions openAIOptions,
        DeepSeekOptions deepSeekOptions)
    {
        var provider = string.IsNullOrWhiteSpace(aiOptions.Provider)
            ? "OpenAI"
            : aiOptions.Provider.Trim();

        if (provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase) ||
            provider.Equals("ChatGPT", StringComparison.OrdinalIgnoreCase) ||
            provider.Equals("GPT", StringComparison.OrdinalIgnoreCase))
        {
            return new AIClientSettings(
                Provider: "OpenAI",
                ConfigurationSection: OpenAIOptions.SectionName,
                ApiKey: openAIOptions.ApiKey,
                Model: openAIOptions.Model,
                BaseUrl: openAIOptions.BaseUrl);
        }

        if (provider.Equals("DeepSeek", StringComparison.OrdinalIgnoreCase))
        {
            return new AIClientSettings(
                Provider: "DeepSeek",
                ConfigurationSection: DeepSeekOptions.SectionName,
                ApiKey: deepSeekOptions.ApiKey,
                Model: deepSeekOptions.Model,
                BaseUrl: deepSeekOptions.BaseUrl);
        }

        throw new InvalidOperationException(
            $"AI:Provider '{provider}' is not supported. Use 'OpenAI' or 'DeepSeek'.");
    }

    private sealed record AIClientSettings(
        string Provider,
        string ConfigurationSection,
        string ApiKey,
        string Model,
        string BaseUrl);
}
