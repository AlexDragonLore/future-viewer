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
        if (!aiOptions.Value.Enabled)
            throw new InvalidOperationException(
                "AI integration is disabled by AI:Enabled.");

        var settings = ResolveSettings(aiOptions.Value, openAIOptions.Value, deepSeekOptions.Value);
        var endpoint = NormalizeAndValidateEndpoint(settings.BaseUrl);
        ValidateOfficialEndpoint(settings.Provider, endpoint);

        Provider = settings.Provider;
        Model = settings.Model;
        Endpoint = endpoint;

        if (string.IsNullOrWhiteSpace(settings.ApiKey))
            throw new InvalidOperationException($"{settings.ConfigurationSection}:ApiKey is not configured");

        if (string.IsNullOrWhiteSpace(settings.Model))
            throw new InvalidOperationException($"{settings.ConfigurationSection}:Model is not configured");

        var clientOptions = new OpenAIClientOptions { Endpoint = endpoint };

        _client = new OpenAIClient(new ApiKeyCredential(settings.ApiKey), clientOptions);
    }

    public string Provider { get; }
    public string Model { get; }
    public Uri Endpoint { get; }

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
                BaseUrl: string.IsNullOrWhiteSpace(openAIOptions.BaseUrl)
                    ? "https://api.openai.com/v1"
                    : openAIOptions.BaseUrl);
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

    private static Uri NormalizeAndValidateEndpoint(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var endpoint) ||
            !string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(endpoint.Host) ||
            !string.IsNullOrEmpty(endpoint.UserInfo) ||
            !string.IsNullOrEmpty(endpoint.Query) ||
            !string.IsNullOrEmpty(endpoint.Fragment))
        {
            throw new InvalidOperationException("AI endpoint must be an exact HTTPS base URL without credentials, query or fragment.");
        }

        return new Uri(endpoint.AbsoluteUri.TrimEnd('/'), UriKind.Absolute);
    }

    private static void ValidateOfficialEndpoint(string provider, Uri endpoint)
    {
        var expected = provider switch
        {
            "OpenAI" => "https://api.openai.com/v1",
            "DeepSeek" => "https://api.deepseek.com",
            _ => throw new InvalidOperationException("Unsupported AI provider endpoint mapping.")
        };
        if (!string.Equals(endpoint.AbsoluteUri.TrimEnd('/'), expected, StringComparison.Ordinal))
            throw new InvalidOperationException("AI endpoint must be the selected provider's official HTTPS endpoint.");
    }

    private sealed record AIClientSettings(
        string Provider,
        string ConfigurationSection,
        string ApiKey,
        string Model,
        string BaseUrl);

}
