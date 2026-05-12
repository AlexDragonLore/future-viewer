namespace FutureViewer.Infrastructure.Payment;

public sealed class YooMoneyOptions
{
    public const string SectionName = "YooMoney";

    public string Receiver { get; set; } = string.Empty;
    public string NotificationSecret { get; set; } = string.Empty;
    public string ReturnUrl { get; set; } = "http://localhost:5173/payment/success";
    public string QuickpayUrl { get; set; } = "https://yoomoney.ru/quickpay/confirm";
    public string QuickpayForm { get; set; } = "button";
    public string PaymentType { get; set; } = "AC";
    public string CurrencyCode { get; set; } = "643";
    public decimal MonthlyPriceAmount { get; set; } = 300m;
    public string Targets { get; set; } = "Доступ Future Viewer Pro";
}
