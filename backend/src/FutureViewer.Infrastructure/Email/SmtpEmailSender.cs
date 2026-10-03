using FutureViewer.DomainServices.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace FutureViewer.Infrastructure.Email;

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(
        IOptions<EmailOptions> options,
        ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured
    {
        get
        {
            if (_options.GetTransport() != "Smtp" || !_options.IsConfigured) return false;
            if (!_options.UseSsl)
                throw new InvalidOperationException("SMTP TLS is required for external email delivery.");
            return true;
        }
    }

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            _logger.LogInformation("Email SMTP not configured; transactional email suppressed");
            return;
        }

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_options.From));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        var secure = ResolveSecureSocketOptions(_options.UseSsl, _options.Port);
        await client.ConnectAsync(_options.Host, _options.Port, secure, ct);
        if (!string.IsNullOrEmpty(_options.Username))
            await client.AuthenticateAsync(_options.Username, _options.Password, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }

    private static SecureSocketOptions ResolveSecureSocketOptions(bool useSsl, int port)
    {
        if (!useSsl) return SecureSocketOptions.None;
        // Port 465 uses implicit TLS; 587/25 require STARTTLS.
        // StartTls (not StartTlsWhenAvailable) fails the connection if the server
        // doesn't advertise STARTTLS, preventing silent downgrade to plaintext.
        return port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
    }
}

public sealed class EmailLinkBuilder : IEmailLinkBuilder
{
    private readonly EmailOptions _options;

    public EmailLinkBuilder(IOptions<EmailOptions> options)
    {
        _options = options.Value;
    }

    public string BuildVerificationLink(string token)
    {
        var baseUrl = _options.FrontendUrl.TrimEnd('/');
        // URL fragments are not sent to the web server and therefore avoid
        // placing bearer tokens in reverse-proxy access logs.
        return $"{baseUrl}/verify-email#token={Uri.EscapeDataString(token)}";
    }

    public string BuildPasswordResetLink(string token)
    {
        var baseUrl = _options.FrontendUrl.TrimEnd('/');
        return $"{baseUrl}/reset-password#token={Uri.EscapeDataString(token)}";
    }
}
