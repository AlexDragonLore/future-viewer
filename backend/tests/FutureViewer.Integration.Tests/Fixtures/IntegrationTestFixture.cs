using FutureViewer.DomainServices.Interfaces;
using FutureViewer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace FutureViewer.Integration.Tests.Fixtures;

public sealed class IntegrationTestFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("future_viewer_tests")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        await DatabaseInitializer.SeedAsync(db);
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
            var dbContextOptions = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (dbContextOptions is not null) services.Remove(dbContextOptions);

            services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(_postgres.GetConnectionString()));

            var aiDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IAIInterpreter));
            if (aiDescriptor is not null) services.Remove(aiDescriptor);
            services.AddSingleton<IAIInterpreter, StubAIInterpreter>();
        });
    }
}
