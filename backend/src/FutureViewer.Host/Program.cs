using System.Text;
using System.Security.Cryptography;
using System.Threading.RateLimiting;
using System.Net;
using FutureViewer.DomainServices.DependencyInjection;
using FutureViewer.Host.Endpoints;
using FutureViewer.Host.Middleware;
using FutureViewer.Infrastructure.Auth;
using FutureViewer.Infrastructure.DependencyInjection;
using FutureViewer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.DataProtection;
using FutureViewer.Host.Auth;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.Limits.MaxRequestBodySize = 1024 * 1024;
});

// Services
builder.Services.AddDomainServices();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.IsDevelopment());
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("FutureViewer");
if (builder.Configuration["DataProtection:KeysPath"] is { Length: > 0 } keysPath)
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
builder.Services.AddSingleton<GuestReadingTickets>();
builder.Services.Configure<SupportOptions>(builder.Configuration.GetSection(SupportOptions.SectionName));

// Forwarded client addresses are security inputs for rate limits and payment
// webhooks. Accept them only from explicitly configured, exact proxy addresses.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    var knownProxyAddresses = (builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
        .Select(value => IPAddress.TryParse(value, out var address)
            && !address.Equals(IPAddress.Any) && !address.Equals(IPAddress.IPv6Any)
                ? address
                : throw new InvalidOperationException("ReverseProxy:KnownProxies must contain exact proxy IP addresses."))
        .ToArray();
    // An empty allowlist must disable processing, never imply trust-all.
    options.ForwardedHeaders = knownProxyAddresses.Length == 0
        ? ForwardedHeaders.None
        : ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = Math.Clamp(builder.Configuration.GetValue("ReverseProxy:ForwardLimit", 1), 1, 5);
    options.KnownProxies.Clear();
    options.KnownIPNetworks.Clear();
    foreach (var address in knownProxyAddresses)
        options.KnownProxies.Add(address);
});

// JWT auth
var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
var jwtOptions = jwtSection.Get<JwtOptions>() ?? new JwtOptions();
var jwtSecret = jwtOptions.Secret;
if (string.IsNullOrEmpty(jwtSecret))
{
    if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
        jwtSecret = new string('x', 32);
    else
        throw new InvalidOperationException(
            "Jwt:Secret is not configured. Set the Jwt:Secret configuration value (or JWT_SECRET env var) outside Development/Testing.");
}
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        opt.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var subject = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                              ?? context.Principal?.FindFirst("sub")?.Value;
                var version = context.Principal?.FindFirst("security_version")?.Value;
                if (!Guid.TryParse(subject, out var userId) || !int.TryParse(version, out var securityVersion))
                {
                    context.Fail("Invalid session claims.");
                    return;
                }

                var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var state = await db.Users.AsNoTracking()
                    .Where(x => x.Id == userId)
                    .Select(x => new { x.AccountStatus, x.SecurityVersion, x.IsAdmin })
                    .SingleOrDefaultAsync(context.HttpContext.RequestAborted);
                var hasAdminClaim = context.Principal?.IsInRole("Admin") == true;
                if (state is null
                    || state.AccountStatus != FutureViewer.Domain.Enums.UserAccountStatus.Active
                    || state.SecurityVersion != securityVersion
                    || (hasAdminClaim && !state.IsAdmin))
                {
                    context.Fail("Session has been revoked.");
                }
            }
        };
    });
builder.Services.AddAuthorization(opt =>
{
    opt.AddPolicy("Admin", p => p.RequireRole("Admin"));
});

// CORS
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                     ?? new[] { "http://localhost:5173" };
builder.Services.AddCors(opt => opt.AddDefaultPolicy(p => p
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()));

// OpenAPI (.NET 10 built-in)
builder.Services.AddOpenApi();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = static async (context, ct) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { error = "rate_limited", message = "Слишком много запросов. Повторите позже." },
            ct);
    };

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetClientPartition(context),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        GetClientPartition(context),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0,
            AutoReplenishment = true
        }));

    options.AddPolicy("guest-reading", context => RateLimitPartition.GetFixedWindowLimiter(
        GetClientPartition(context),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 3,
            Window = TimeSpan.FromHours(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));

    options.AddPolicy("ai", context => RateLimitPartition.GetFixedWindowLimiter(
        GetAuthenticatedOrClientPartition(context),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 12,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));

    options.AddPolicy("privacy", context => RateLimitPartition.GetFixedWindowLimiter(
        GetAuthenticatedOrClientPartition(context),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromHours(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));

    options.AddPolicy("webhook", context => RateLimitPartition.GetFixedWindowLimiter(
        GetClientPartition(context),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 120,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});

var app = builder.Build();

// Middleware
app.UseForwardedHeaders();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<ExceptionHandlerMiddleware>();

if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

// Endpoints
app.MapGet("/health", async (AppDbContext db, CancellationToken ct) =>
{
    var databaseReady = await db.Database.CanConnectAsync(ct);
    return databaseReady
        ? Results.Ok(new { status = "ok", database = "ready", time = DateTime.UtcNow })
        : Results.Json(
            new { status = "degraded", database = "unavailable", time = DateTime.UtcNow },
            statusCode: StatusCodes.Status503ServiceUnavailable);
}).DisableRateLimiting();
app.MapPublic();
app.MapLegalDocuments();
app.MapReadings();
app.MapAuth();
app.MapCards();
app.MapSubscription();
app.MapPayments();
app.MapPrivacy();
app.MapFeedbacks();
app.MapProfile();
app.MapLeaderboard();
app.MapAchievements();
app.MapAdmin();
app.MapAnnouncements();

// Migrations + seed at startup (skip in Testing env — integration tests manage their own DB)
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
await DatabaseInitializer.InitializeAsync(db, config);
}

app.Run();

static string GetAuthenticatedOrClientPartition(HttpContext context)
{
    var subject = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                  ?? context.User.FindFirst("sub")?.Value;
    return !string.IsNullOrWhiteSpace(subject)
        ? $"user:{subject}"
        : GetClientPartition(context);
}

static string GetClientPartition(HttpContext context)
{
    // Only trusted ForwardedHeaders middleware may replace this peer address.
    var address = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    return "client:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(address)));
}

public partial class Program { }
