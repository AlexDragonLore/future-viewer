using FluentAssertions;
using FutureViewer.Infrastructure.AI;
using FutureViewer.Infrastructure.Compliance;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class ProcessorRegistryGuardTests
{
    [Fact]
    public void Unknown_endpoint_is_blocked_even_when_another_provider_is_approved()
    {
        var directory = Directory.CreateTempSubdirectory("fv-processor-registry-");
        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, "processors.json"),
                """
                {
                  "providers": [{
                    "provider_name": "OpenAI API",
                    "provider_type": "ai",
                    "legal_entity": "Reviewed entity",
                    "country": "US",
                    "endpoint": "https://api.openai.com/v1",
                    "retention": "contractual-retention-reference",
                    "uses_data_for_training": false,
                    "cross_border_transfer": true,
                    "contract_reference": "contract-1",
                    "enabled": true,
                    "manual_approved": true
                  }]
                }
                """);
            var guard = new ProcessorRegistryGuard(
                Options.Create(new AIOptions { RegistryPath = "processors.json" }),
                new TestHostEnvironment(directory.FullName));

            guard.Invoking(x => x.EnsureApproved(
                    "ai", "OpenAI API", "https://unreviewed.example/v1"))
                .Should().Throw<InvalidOperationException>()
                .WithMessage("*not registered*");

            guard.Invoking(x => x.EnsureApproved(
                    "ai", "OpenAI", "https://api.openai.com/v1"))
                .Should().Throw<InvalidOperationException>()
                .WithMessage("*not registered*");

            guard.Invoking(x => x.EnsureApproved(
                    "ai", "OpenAI API", "https://api.openai.com/v1"))
                .Should().NotThrow();
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private sealed class TestHostEnvironment(string contentRoot) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "FutureViewer.Tests";
        public string ContentRootPath { get; set; } = contentRoot;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
