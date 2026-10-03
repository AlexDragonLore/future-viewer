using FutureViewer.DomainServices;
using FutureViewer.DomainServices.Interfaces;
using Microsoft.Extensions.Logging;

namespace FutureViewer.Infrastructure.BackgroundServices;

public sealed class AccountDeletionProcessor
{
    private readonly IPrivacyRepository _privacy;
    private readonly PrivacyOptions _options;
    private readonly ILogger<AccountDeletionProcessor> _logger;

    public AccountDeletionProcessor(
        IPrivacyRepository privacy,
        PrivacyOptions options,
        ILogger<AccountDeletionProcessor> logger)
    {
        _privacy = privacy;
        _options = options;
        _logger = logger;
    }

    public async Task<int> ProcessBatchAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var recovered = await _privacy.RecoverInterruptedDeletionJobsAsync(
            now.AddMinutes(-Math.Clamp(_options.DeletionJobLeaseMinutes, 1, 1440)),
            now.AddMinutes(-Math.Clamp(_options.DeletionRetryDelayMinutes, 1, 1440)),
            ct);
        if (recovered > 0)
            _logger.LogWarning("Requeued {Count} interrupted account deletion jobs", recovered);

        var jobs = await _privacy.GetPendingDeletionJobsAsync(
            now,
            Math.Clamp(_options.DeletionBatchSize, 1, 100),
            ct);
        var completed = 0;

        foreach (var job in jobs)
        {
            ct.ThrowIfCancellationRequested();
            if (!await _privacy.TryMarkDeletionJobRunningAsync(job.Id, DateTime.UtcNow, ct))
                continue;

            try
            {
                await _privacy.CompleteAccountDeletionAsync(job.Id, DateTime.UtcNow, ct);
                completed++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // Deliberately log only opaque job metadata. Exception messages and
                // database command parameters can contain data scheduled for deletion.
                _logger.LogError(
                    "Account deletion failed for JobId {JobId}; ErrorType {ErrorType}",
                    job.Id,
                    exception.GetType().Name);
                await _privacy.MarkDeletionJobFailedAsync(job.Id, "processor_error", ct);
            }
        }

        if (completed > 0)
            _logger.LogInformation("Completed {Count} account deletion jobs", completed);
        return completed;
    }
}
