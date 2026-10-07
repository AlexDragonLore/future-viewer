namespace FutureViewer.DomainServices;

public static class GuestReadingRetention
{
    public const int Hours = 24;
    public static TimeSpan Duration => TimeSpan.FromHours(Hours);
}
