using MimeKit;

namespace FutureViewer.Infrastructure.Email;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string Transport { get; set; } = "Smtp";
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public bool UseSsl { get; set; } = true;
    public string FrontendUrl { get; set; } = "http://localhost:5173";

    public bool IsConfigured => GetTransport() switch
    {
        "Smtp" => !string.IsNullOrWhiteSpace(Host),
        "RegruWebmail" => !string.IsNullOrWhiteSpace(Password)
            && !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(From)
            && !Username.Contains('\r') && !Username.Contains('\n')
            && !From.Contains('\r') && !From.Contains('\n')
            && MailboxAddress.TryParse(Username, out var login)
            && login.Address.Contains('@')
            && string.Equals(login.Address, Username.Trim(), StringComparison.OrdinalIgnoreCase)
            && MailboxAddress.TryParse(From, out var sender)
            && string.Equals(login.Address, sender.Address, StringComparison.OrdinalIgnoreCase),
        _ => false
    };

    internal string GetTransport()
    {
        if (string.Equals(Transport?.Trim(), "Smtp", StringComparison.OrdinalIgnoreCase)) return "Smtp";
        if (string.Equals(Transport?.Trim(), "RegruWebmail", StringComparison.OrdinalIgnoreCase)) return "RegruWebmail";
        throw new InvalidOperationException("Email:Transport must be Smtp or RegruWebmail.");
    }
}
