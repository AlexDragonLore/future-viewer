namespace FutureViewer.DomainServices.Interfaces;

public interface IEmailSender
{
    bool IsConfigured { get; }

    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
}

public interface IEmailLinkBuilder
{
    string BuildVerificationLink(string token);
    string BuildPasswordResetLink(string token);
}
