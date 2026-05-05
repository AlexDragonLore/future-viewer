namespace FutureViewer.Infrastructure.Email;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string From { get; set; } = "no-reply@vualgryaduschego.ru";
    public bool UseSsl { get; set; } = true;
    public string FrontendUrl { get; set; } = "http://localhost:5173";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host);
}
