using System.Text.Json;
using System.Text.Json.Serialization;
using FutureViewer.Infrastructure.AI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace FutureViewer.Infrastructure.Compliance;

/// <summary>
/// Fail-closed runtime guard for every optional external processor. A feature
/// flag or secret alone is never sufficient to permit a disclosure.
/// </summary>
public interface IProcessorRegistryGuard
{
    void EnsureApproved(
        string providerType,
        string providerName,
        string endpoint,
        bool prohibitTraining = true);
}

public sealed class ProcessorRegistryGuard : IProcessorRegistryGuard
{
    private readonly string _path;

    public ProcessorRegistryGuard(IOptions<AIOptions> options, IHostEnvironment environment)
    {
        _path = Path.IsPathRooted(options.Value.RegistryPath)
            ? options.Value.RegistryPath
            : Path.Combine(environment.ContentRootPath, options.Value.RegistryPath);
    }

    public void EnsureApproved(
        string providerType,
        string providerName,
        string endpoint,
        bool prohibitTraining = true)
    {
        if (!File.Exists(_path))
            throw new InvalidOperationException("Processor registry is missing; external transfer is blocked.");

        ProcessorRegistry? registry;
        try
        {
            registry = JsonSerializer.Deserialize<ProcessorRegistry>(
                File.ReadAllText(_path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("Processor registry is invalid; external transfer is blocked.");
        }

        if (!TryNormalizeEndpoint(endpoint, providerType, out var expected))
            throw new InvalidOperationException("External processor endpoint is invalid.");

        var record = registry?.Providers.FirstOrDefault(x =>
            string.Equals(x.ProviderType, providerType, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.ProviderName, providerName, StringComparison.OrdinalIgnoreCase)
            && TryNormalizeEndpoint(x.Endpoint, providerType, out var configured)
            && string.Equals(configured, expected, StringComparison.OrdinalIgnoreCase));

        if (record is null)
            throw new InvalidOperationException("External processor endpoint is not registered; transfer is blocked.");
        if (!record.Enabled || !record.ManualApproved)
            throw new InvalidOperationException("External processor is not enabled and manually approved.");
        if (string.IsNullOrWhiteSpace(record.LegalEntity)
            || string.IsNullOrWhiteSpace(record.Country)
            || string.IsNullOrWhiteSpace(record.Retention)
            || string.IsNullOrWhiteSpace(record.ContractReference)
            || record.UsesDataForTraining is null
            || record.CrossBorderTransfer is null)
        {
            throw new InvalidOperationException("External processor registry entry is incomplete.");
        }

        if (prohibitTraining && record.UsesDataForTraining.Value)
            throw new InvalidOperationException("Processor training on service user data is prohibited.");
    }

    private static bool TryNormalizeEndpoint(
        string? value,
        string providerType,
        out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value)
            || !Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || string.IsNullOrWhiteSpace(uri.Scheme)
            || string.IsNullOrWhiteSpace(uri.Host)
            || !string.IsNullOrWhiteSpace(uri.UserInfo)
            || !string.IsNullOrWhiteSpace(uri.Query)
            || !string.IsNullOrWhiteSpace(uri.Fragment))
        {
            return false;
        }

        var allowedScheme = string.Equals(providerType, "email", StringComparison.OrdinalIgnoreCase)
            ? uri.Scheme is "smtps" or "smtp+starttls"
            : string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        if (!allowedScheme) return false;

        normalized = uri.AbsoluteUri.TrimEnd('/');
        return true;
    }

    private sealed class ProcessorRegistry
    {
        [JsonPropertyName("providers")]
        public List<ProcessorRecord> Providers { get; init; } = [];
    }

    private sealed class ProcessorRecord
    {
        [JsonPropertyName("provider_name")]
        public string ProviderName { get; init; } = string.Empty;
        [JsonPropertyName("provider_type")]
        public string ProviderType { get; init; } = string.Empty;
        [JsonPropertyName("legal_entity")]
        public string? LegalEntity { get; init; }
        [JsonPropertyName("country")]
        public string? Country { get; init; }
        [JsonPropertyName("endpoint")]
        public string? Endpoint { get; init; }
        [JsonPropertyName("retention")]
        public string? Retention { get; init; }
        [JsonPropertyName("uses_data_for_training")]
        public bool? UsesDataForTraining { get; init; }
        [JsonPropertyName("cross_border_transfer")]
        public bool? CrossBorderTransfer { get; init; }
        [JsonPropertyName("contract_reference")]
        public string? ContractReference { get; init; }
        [JsonPropertyName("enabled")]
        public bool Enabled { get; init; }
        [JsonPropertyName("manual_approved")]
        public bool ManualApproved { get; init; }
    }
}
