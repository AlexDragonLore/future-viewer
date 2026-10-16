using System.Diagnostics;

namespace FutureViewer.Infrastructure.BackgroundServices;

public static class RetentionCleanupBatch
{
    public const int BatchSize = 500;
    public const int MaxBatches = 10;
    private static readonly TimeSpan RunBudget = TimeSpan.FromSeconds(10);

    public static async Task<int> RunAsync(
        Func<DateTime, CancellationToken, int, Task<int>> purge,
        DateTime cutoff,
        CancellationToken ct)
    {
        var timer = Stopwatch.StartNew();
        var total = 0;
        for (var batch = 0; batch < MaxBatches && timer.Elapsed < RunBudget; batch++)
        {
            ct.ThrowIfCancellationRequested();
            var deleted = await purge(cutoff, ct, BatchSize);
            total += deleted;
            if (deleted < BatchSize) break;
        }
        return total;
    }
}
