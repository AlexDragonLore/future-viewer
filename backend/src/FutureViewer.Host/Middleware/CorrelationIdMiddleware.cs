using System.Text.RegularExpressions;

namespace FutureViewer.Host.Middleware;

public sealed partial class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-ID";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var supplied = context.Request.Headers[HeaderName].FirstOrDefault();
        var correlationId = !string.IsNullOrWhiteSpace(supplied) && SafeIdRegex().IsMatch(supplied)
            ? supplied
            : Guid.NewGuid().ToString("N");

        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;
        await _next(context);
    }

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._-]{7,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeIdRegex();
}
