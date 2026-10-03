namespace FutureViewer.Infrastructure.Payment;

internal static class PaymentEndpointSafety
{
    private static bool Matches(string value, string host, string path) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps && uri.Port == 443
        && uri.Host.Equals(host, StringComparison.OrdinalIgnoreCase)
        && uri.AbsolutePath == path && string.IsNullOrEmpty(uri.UserInfo)
        && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment);

    public static bool IsYukassaApi(string value) => Matches(value, "api.yookassa.ru", "/v3/");
    public static bool IsYooMoneyCheckout(string value) => Matches(value, "yoomoney.ru", "/quickpay/confirm");

    public static void EnsureYukassaApi(string value)
    {
        if (!IsYukassaApi(value)) throw new InvalidOperationException("Invalid YooKassa API endpoint.");
    }

    public static void EnsureYooMoneyCheckout(string value)
    {
        if (!IsYooMoneyCheckout(value)) throw new InvalidOperationException("Invalid YooMoney checkout endpoint.");
    }
}
