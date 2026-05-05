using System.Collections.Concurrent;
using FutureViewer.DomainServices.Interfaces;

namespace FutureViewer.Integration.Tests.Fixtures;

public sealed record CapturedEmail(string To, string Subject, string HtmlBody);

public sealed class CapturingEmailSender : IEmailSender
{
    public ConcurrentQueue<CapturedEmail> Sent { get; } = new();

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        Sent.Enqueue(new CapturedEmail(to, subject, htmlBody));
        return Task.CompletedTask;
    }

    public CapturedEmail? LastFor(string to) =>
        Sent.Reverse().FirstOrDefault(e => string.Equals(e.To, to, StringComparison.OrdinalIgnoreCase));
}

public sealed class FakeEmailLinkBuilder : IEmailLinkBuilder
{
    public string BuildVerificationLink(string token) => $"http://test/verify-email?token={token}";
    public string BuildPasswordResetLink(string token) => $"http://test/reset-password?token={token}";
}
