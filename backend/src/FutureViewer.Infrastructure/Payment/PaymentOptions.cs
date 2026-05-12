namespace FutureViewer.Infrastructure.Payment;

public sealed class PaymentOptions
{
    public const string SectionName = "Payment";

    public string Provider { get; set; } = "Yukassa";
}
