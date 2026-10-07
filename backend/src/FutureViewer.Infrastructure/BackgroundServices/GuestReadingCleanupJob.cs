using FutureViewer.DomainServices;
using FutureViewer.DomainServices.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FutureViewer.Infrastructure.BackgroundServices;

public sealed class GuestReadingCleanupJob : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<GuestReadingCleanupJob> _logger;

    public GuestReadingCleanupJob(
        IServiceScopeFactory scopes,
        ILogger<GuestReadingCleanupJob> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IPrivacyRepository>();
                var deleted = await repository.PurgeGuestReadingsBeforeAsync(
                    DateTime.UtcNow - GuestReadingRetention.Duration,
                    stoppingToken);
                if (deleted > 0)
                    _logger.LogInformation("Guest reading retention cleanup deleted {DeletedCount} readings", deleted);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError("Guest reading retention cleanup failed; errorType={ErrorType}", exception.GetType().Name);
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }
}
