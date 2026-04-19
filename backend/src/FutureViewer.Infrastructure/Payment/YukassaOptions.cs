namespace FutureViewer.Infrastructure.Payment;

public sealed class YukassaOptions
{
    public const string SectionName = "Yukassa";

    public string ShopId { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string ReturnUrl { get; set; } = "http://localhost:5173/payment/success";
    public string Currency { get; set; } = "RUB";
    public decimal MonthlyPriceAmount { get; set; } = 300m;
    public string ApiBaseUrl { get; set; } = "https://api.yookassa.ru/v3/";
}
