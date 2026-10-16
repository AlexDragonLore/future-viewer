using FluentAssertions;
using FutureViewer.Infrastructure.BackgroundServices;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class RetentionCleanupBatchTests
{
    [Fact]
    public async Task Run_uses_fixed_cutoff_and_stops_at_row_budget_even_with_a_backlog()
    {
        var cutoff = DateTime.UtcNow.AddDays(-30);
        var calls = 0;
        var deleted = await RetentionCleanupBatch.RunAsync((value, _, take) =>
        {
            value.Should().Be(cutoff);
            take.Should().Be(500);
            calls++;
            return Task.FromResult(take);
        }, cutoff, CancellationToken.None);
        calls.Should().Be(10);
        deleted.Should().Be(5000);
    }

    [Fact]
    public async Task Run_stops_after_partial_batch_and_honors_cancellation_before_work()
    {
        var calls = 0;
        Task<int> Purge(DateTime _, CancellationToken ct, int take)
        {
            calls++;
            return Task.FromResult(calls == 1 ? take : 7);
        }
        (await RetentionCleanupBatch.RunAsync(Purge, DateTime.UtcNow, CancellationToken.None)).Should().Be(507);
        calls.Should().Be(2);
        var action = () => RetentionCleanupBatch.RunAsync(Purge, DateTime.UtcNow, new CancellationToken(true));
        await action.Should().ThrowAsync<OperationCanceledException>();
        calls.Should().Be(2);
    }
}
