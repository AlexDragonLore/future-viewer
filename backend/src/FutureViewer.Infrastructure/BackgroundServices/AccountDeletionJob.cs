using FutureViewer.DomainServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FutureViewer.Infrastructure.BackgroundServices;

public sealed class AccountDeletionJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PrivacyOptions _options;
    private readonly ILogger<AccountDeletionJob> _logger;

    public AccountDeletionJob(
        IServiceScopeFactory scopeFactory,
        PrivacyOptions options,
        ILogger<AccountDeletionJob> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Clamp(_options.DeletionPollIntervalSeconds, 10, 3600));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<AccountDeletionProcessor>();
                await processor.ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    "Account deletion worker iteration failed; ErrorType {ErrorType}",
                    exception.GetType().Name);
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
