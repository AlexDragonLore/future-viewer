using System.Text;
using FutureViewer.DomainServices.DependencyInjection;
using FutureViewer.Host.Endpoints;
using FutureViewer.Host.Middleware;
using FutureViewer.Infrastructure.Auth;
using FutureViewer.Infrastructure.DependencyInjection;
using FutureViewer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddDomainServices();
builder.Services.AddInfrastructure(builder.Configuration);

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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
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

var app = builder.Build();

// Middleware
app.UseMiddleware<ExceptionHandlerMiddleware>();

if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Endpoints
app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }));
app.MapReadings();
app.MapAuth();
app.MapCards();
app.MapSubscription();
app.MapPayments();
app.MapFeedbacks();
app.MapLeaderboard();
app.MapAchievements();
app.MapTelegram();
app.MapAdmin();

// Migrations + seed at startup (skip in Testing env — integration tests manage their own DB)
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    await DatabaseInitializer.InitializeAsync(db, config);
}

app.Run();

public partial class Program { }
