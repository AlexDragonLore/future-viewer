using FluentAssertions;
using FutureViewer.Infrastructure.AI;
using Microsoft.Extensions.Options;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class AIChatClientFactoryTests
{
    private const string TestApiKey = "fixture-not-a-real-api-key";

    [Theory]
    [InlineData("OpenAI", "https://api.openai.com/v1", "gpt-4o")]
    [InlineData("DeepSeek", "https://api.deepseek.com", "deepseek-v4-flash")]
    public void Official_provider_with_credentials_works_without_documentary_registry_approval(
        string provider, string endpoint, string model)
    {
        var factory = Create(provider, endpoint);

        factory.Provider.Should().Be(provider);
        factory.Endpoint.AbsoluteUri.TrimEnd('/').Should().Be(endpoint);
        factory.Model.Should().Be(model);
        factory.CreateChatClient().Should().NotBeNull();
    }

    [Theory]
    [InlineData("OpenAI", "http://api.openai.com/v1")]
    [InlineData("OpenAI", "https://api.openai.com.attacker.test/v1")]
    [InlineData("OpenAI", "https://recipient@example.test@api.openai.com/v1")]
    [InlineData("OpenAI", "https://api.openai.com/v1?email=private@example.test")]
    [InlineData("OpenAI", "https://api.openai.com/v1#private-token")]
    [InlineData("OpenAI", "https://api.openai.com:8443/v1")]
    [InlineData("OpenAI", "https://api.openai.com/v1/collect")]
    [InlineData("OpenAI", "https://api.deepseek.com")]
    [InlineData("DeepSeek", "https://api.deepseek.com/collect")]
    public void Unsafe_or_unrelated_endpoint_is_rejected_without_disclosing_credentials_or_input(
        string provider, string endpoint)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Create(provider, endpoint));

        exception.Message.Should().NotContain(TestApiKey)
            .And.NotContain("private@example.test")
            .And.NotContain("private-token");
    }

    [Fact]
    public void Explicit_operational_disable_is_respected_even_with_credentials()
    {
        Assert.Throws<InvalidOperationException>(() => Create("OpenAI", "https://api.openai.com/v1", enabled: false));
    }

    [Theory]
    [InlineData("OpenAI")]
    [InlineData("DeepSeek")]
    public void Missing_provider_key_is_not_a_working_integration(string provider)
    {
        var endpoint = provider == "OpenAI" ? "https://api.openai.com/v1" : "https://api.deepseek.com";

        Assert.Throws<InvalidOperationException>(() => Create(provider, endpoint, key: ""));
    }

    [Fact]
    public void Missing_model_is_not_a_working_integration()
    {
        Assert.Throws<InvalidOperationException>(() => Create("OpenAI", "https://api.openai.com/v1", model: ""));
    }

    private static AIChatClientFactory Create(
        string provider, string endpoint, bool enabled = true, string key = TestApiKey, string? model = null) =>
        new(Options.Create(new AIOptions
        {
            Provider = provider,
            Enabled = enabled,
            RegistryPath = "missing-documentary-registry.json"
        }), Options.Create(new OpenAIOptions
        {
            ApiKey = key,
            Model = model ?? "gpt-4o",
            BaseUrl = endpoint
        }), Options.Create(new DeepSeekOptions
        {
            ApiKey = key,
            Model = model ?? "deepseek-v4-flash",
            BaseUrl = endpoint
        }));
}
