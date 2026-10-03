using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.Interfaces;
using FutureViewer.Infrastructure.Persistence;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace FutureViewer.Integration.Tests.Fixtures;

public sealed class IntegrationTestFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private int _clientSequence;
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("future_viewer_tests")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    public CapturingEmailSender EmailSender { get; } = new();

    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        // Independent simulated clients should not share TestServer's null peer IP.
        // Keep rate limiting enabled, including repeated requests from one client.
        client.DefaultRequestHeaders.Add("X-Test-Client-IP", $"2001:db8::{Interlocked.Increment(ref _clientSequence):x}");
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        await DatabaseInitializer.SeedAsync(db);
        var now = DateTime.UtcNow.AddMinutes(-1);
        db.LegalDocuments.AddRange(Enum.GetValues<LegalDocumentType>().Select(type => new LegalDocument
        {
            DocumentType = type,
            Version = AuthTestExtensions.LegalDocumentVersion,
            ContentHash = new string('a', 64),
            PublishedAt = now,
            EffectiveAt = now,
            IsActive = true
        }));
        await db.SaveChangesAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Environment.SetEnvironmentVariable("Jwt__Secret", "test-secret-for-integration-tests-32-chars-minimum-ok");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "future-viewer-tests");
        Environment.SetEnvironmentVariable("Jwt__Audience", "future-viewer-tests");
        Environment.SetEnvironmentVariable("OpenAI__ApiKey", "stub");
        Environment.SetEnvironmentVariable("OpenAI__Model", "stub-model");
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", _postgres.GetConnectionString());

        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.AddTransient<IStartupFilter, TestClientAddressStartupFilter>();
            var dbContextOptions = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (dbContextOptions is not null) services.Remove(dbContextOptions);

            services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(_postgres.GetConnectionString()));

            var aiDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IAIInterpreter));
            if (aiDescriptor is not null) services.Remove(aiDescriptor);
            services.AddSingleton<IAIInterpreter, StubAIInterpreter>();

            var validatorDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IAIQuestionValidator));
            if (validatorDescriptor is not null) services.Remove(validatorDescriptor);
            services.AddSingleton<IAIQuestionValidator, StubQuestionValidator>();

            var memoryDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IAIMemoryExtractor));
            if (memoryDescriptor is not null) services.Remove(memoryDescriptor);
            services.AddSingleton<IAIMemoryExtractor, StubMemoryExtractor>();

            var scorerDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IFeedbackScorer));
            if (scorerDescriptor is not null) services.Remove(scorerDescriptor);
            services.AddSingleton<IFeedbackScorer, StubFeedbackScorer>();

            var emailDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IEmailSender));
            if (emailDescriptor is not null) services.Remove(emailDescriptor);
            services.AddSingleton<IEmailSender>(EmailSender);

            var linkDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IEmailLinkBuilder));
            if (linkDescriptor is not null) services.Remove(linkDescriptor);
            services.AddSingleton<IEmailLinkBuilder, FakeEmailLinkBuilder>();
        });
    }

    private sealed class TestClientAddressStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                if (IPAddress.TryParse(context.Request.Headers["X-Test-Client-IP"].FirstOrDefault(), out var address))
                    context.Connection.RemoteIpAddress = address;
                context.Request.Headers.Remove("X-Test-Client-IP");
                await nextMiddleware(context);
            });
            next(app);
        };
    }
}
