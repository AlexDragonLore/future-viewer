using FluentAssertions;
using FutureViewer.Infrastructure.Email;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class SmtpEmailSenderTests
{
    [Fact]
    public async Task Send_rejects_plaintext_before_any_network_delivery()
    {
        var sender = Create(new EmailOptions { Host = "mail.example.test", UseSsl = false });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sender.SendAsync("recipient@example.test", "Verification", "<p>Private link</p>"));
    }

    [Fact]
    public void Configured_tls_smtp_does_not_depend_on_documentary_registry_approval()
    {
        Create(new EmailOptions { Host = "mail.example.test", UseSsl = true }).IsConfigured.Should().BeTrue();
    }

    private static SmtpEmailSender Create(EmailOptions options) =>
        new(Options.Create(options), NullLogger<SmtpEmailSender>.Instance);
}
