using FutureViewer.DomainServices;
using FutureViewer.DomainServices.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FutureViewer.Infrastructure.BackgroundServices;

public sealed class DataSubjectRequestDeadlineJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly PrivacyOptions _options;
    private readonly ILogger<DataSubjectRequestDeadlineJob> _logger;

    public DataSubjectRequestDeadlineJob(
        IServiceScopeFactory scopes,
        PrivacyOptions options,
        ILogger<DataSubjectRequestDeadlineJob> logger)
    {
        _scopes = scopes;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(
            Math.Max(300, _options.DeadlineAlertPollIntervalSeconds));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IPrivacyRepository>();
                var dueBefore = DateTime.UtcNow.AddDays(
                    Math.Max(1, _options.DataSubjectRequestAlertDays));
                var count = await repository.CountOpenRequestsDueBeforeAsync(dueBefore, stoppingToken);
                if (count > 0)
                {
                    _logger.LogWarning(
                        "Data-subject request deadline alert: openDueSoonCount={OpenDueSoonCount}; alertWindowDays={AlertWindowDays}",
                        count,
                        Math.Max(1, _options.DataSubjectRequestAlertDays));
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    "Data-subject deadline check failed; errorType={ErrorType}",
                    ex.GetType().Name);
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
