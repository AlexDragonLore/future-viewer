namespace FutureViewer.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "future-viewer";
    public string Audience { get; set; } = "future-viewer";
    public int ExpiresMinutes { get; set; } = 60 * 24 * 7; // 7 days
}
