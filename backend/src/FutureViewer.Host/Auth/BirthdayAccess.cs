using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FutureViewer.DomainServices.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace FutureViewer.Host.Auth;

public sealed class BirthdayOptions
{
    public string KeyHash { get; set; } = "";
    public string CertificatePath { get; set; } = "";
    public string PostcardPath { get; set; } = "";
}

public sealed record BirthdayTicket(int Completed, string KeyVersion);
public sealed record BirthdaySession(string Ticket, int CompletedSteps, int TotalSteps, int Score, BirthdayQuestion? Question);

public sealed class BirthdayAccess(IDataProtectionProvider provider, IOptions<BirthdayOptions> options)
{
    private readonly ITimeLimitedDataProtector _protector =
        provider.CreateProtector("FutureViewer.Birthday.v1").ToTimeLimitedDataProtector();

    public bool Configured => options.Value.KeyHash.Length == 64
        && options.Value.KeyHash.All(Uri.IsHexDigit)
        && File.Exists(options.Value.CertificatePath)
        && File.Exists(options.Value.PostcardPath);

    public bool Accepts(string? key)
    {
        if (!Configured || string.IsNullOrWhiteSpace(key) || key.Length > 256) return false;
        var actual = SHA256.HashData(Encoding.UTF8.GetBytes(key.Trim()));
        return CryptographicOperations.FixedTimeEquals(actual, Convert.FromHexString(options.Value.KeyHash));
    }

    public BirthdaySession Issue(int completed, DateTimeOffset? expiresAt = null)
    {
        var ticket = _protector.Protect(
            JsonSerializer.Serialize(new BirthdayTicket(completed, options.Value.KeyHash)),
            expiresAt ?? DateTimeOffset.UtcNow.AddDays(7));
        return Describe(ticket, completed);
    }

    public BirthdayTicket? Read(string? ticket, out DateTimeOffset expiresAt)
    {
        expiresAt = default;
        if (!Configured || string.IsNullOrWhiteSpace(ticket) || ticket.Length > 4096) return null;
        try
        {
            var state = JsonSerializer.Deserialize<BirthdayTicket>(_protector.Unprotect(ticket, out expiresAt));
            return state is not null && state.Completed >= 0 && state.Completed <= BirthdayGame.TotalStages
                && state.KeyVersion == options.Value.KeyHash ? state : null;
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException)
        {
            return null;
        }
    }

    public BirthdaySession Describe(string ticket, int completed) =>
        new(ticket, completed, BirthdayGame.TotalStages, completed * BirthdayGame.PointsPerStage, BirthdayGame.Question(completed));

    public string? AssetPath(string kind) => kind switch
    {
        "certificate" => options.Value.CertificatePath,
        "postcard" => options.Value.PostcardPath,
        _ => null
    };
}
