using FutureViewer.DomainServices;
using FutureViewer.DomainServices.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FutureViewer.Infrastructure.BackgroundServices;

public sealed class RetentionCleanupJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly PrivacyOptions _options;
    private readonly ILogger<RetentionCleanupJob> _logger;

    public RetentionCleanupJob(
        IServiceScopeFactory scopes,
        PrivacyOptions options,
        ILogger<RetentionCleanupJob> logger)
    {
        _scopes = scopes;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(3600, _options.RetentionPollIntervalSeconds));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IPrivacyRepository>();
                var now = DateTime.UtcNow;
                var transientDeleted = await repository.PurgeUnsavedReadingsBeforeAsync(
                    now.AddHours(-Math.Max(24, _options.UnsavedReadingRetentionHours)),
                    stoppingToken);
                var auditDeleted = await repository.PurgeAuditEventsBeforeAsync(
                    now.AddDays(-Math.Max(30, _options.AuditEventRetentionDays)),
                    stoppingToken);
                if (transientDeleted > 0 || auditDeleted > 0)
                {
                    _logger.LogInformation(
                        "Retention cleanup completed: transientReadings={TransientReadings}; auditEvents={AuditEvents}",
                        transientDeleted,
                        auditDeleted);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError("Retention cleanup failed; errorType={ErrorType}", ex.GetType().Name);
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
