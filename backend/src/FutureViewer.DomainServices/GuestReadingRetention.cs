namespace FutureViewer.DomainServices;

public static class GuestReadingRetention
{
    public static TimeSpan AccessDuration => TimeSpan.FromHours(24);
    public static TimeSpan ContentDuration => TimeSpan.FromDays(30);
}
