namespace FutureViewer.Infrastructure.Payment;

public sealed class PaymentOptions
{
    public const string SectionName = "Payment";

    /// <summary>
    /// Operational switches. Availability also requires actual provider credentials.
    /// </summary>
    public bool Enabled { get; set; } = true;
    public bool WebhookEnabled { get; set; } = true;
    public string Provider { get; set; } = "Yukassa";
}
