namespace FutureViewer.Host.Middleware;

public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] =
            "accelerometer=(), autoplay=(), camera=(), geolocation=(), gyroscope=(), microphone=(), payment=(), usb=()";
        headers["X-Frame-Options"] = "DENY";
        headers["Content-Security-Policy"] =
            "default-src 'self'; base-uri 'none'; object-src 'none'; frame-ancestors 'none'; " +
            "form-action 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data:; font-src 'self'; connect-src 'self'; media-src 'self'; worker-src 'self'; " +
            "upgrade-insecure-requests";
        headers["Cross-Origin-Opener-Policy"] = "same-origin";
        headers["Cross-Origin-Resource-Policy"] = "same-origin";
        if (context.Request.Path.StartsWithSegments("/api"))
            headers["Cache-Control"] = "no-store";

        await _next(context);
    }
}
