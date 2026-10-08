using FluentAssertions;
using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.Exceptions;
using FutureViewer.DomainServices.Interfaces;
using FutureViewer.DomainServices.Services;
using FutureViewer.Infrastructure.Persistence;
using FutureViewer.Integration.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class ReadingAccessConcurrencyTests(IntegrationTestFixture fixture) : IClassFixture<IntegrationTestFixture>
{
    [Theory]
    [InlineData(SpreadType.ThreeCard)]
    [InlineData(SpreadType.SingleCard)]
    public async Task Concurrent_readings_consume_only_one_free_allowance(SpreadType spread)
    {
        var user = await CreateUserAsync();
        using var firstScope = fixture.Services.CreateScope();
        using var secondScope = fixture.Services.CreateScope();
        var barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var arrived = 0;
        async Task<bool> Save(IServiceScope scope)
        {
            var service = scope.ServiceProvider.GetRequiredService<SubscriptionService>();
            await service.EnsureReadingAllowedAsync(user.Id, spread);
            if (Interlocked.Increment(ref arrived) == 2) barrier.SetResult();
            await barrier.Task;
            try
            {
                await service.AddReadingAsync(new Reading { UserId = user.Id, SpreadType = spread, Question = "test" });
                return true;
            }
            catch (DomainException ex) when (ex is SubscriptionRequiredException or QuotaExceededException)
            {
                return false;
            }
        }

        (await Task.WhenAll(Save(firstScope), Save(secondScope))).Should().BeEquivalentTo([true, false]);
        using var checkScope = fixture.Services.CreateScope();
        var db = checkScope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Readings.CountAsync(r => r.UserId == user.Id)).Should().Be(1);
        var stored = await db.Users.SingleAsync(u => u.Id == user.Id);
        stored.HasUsedIntroReading.Should().BeTrue();
        stored.LastReadingAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Lock_refreshes_preloaded_user_after_intro_content_was_erased()
    {
        var user = await CreateUserAsync();
        using var firstScope = fixture.Services.CreateScope();
        using var secondScope = fixture.Services.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<SubscriptionService>();
        var second = secondScope.ServiceProvider.GetRequiredService<SubscriptionService>();
        // Both scopes track the account before the first claim changes it.
        await first.EnsureReadingAllowedAsync(user.Id, SpreadType.ThreeCard);
        await second.EnsureReadingAllowedAsync(user.Id, SpreadType.ThreeCard);
        var reading = new Reading { UserId = user.Id, SpreadType = SpreadType.ThreeCard, Question = "test", SavedToHistory = true };
        await first.AddReadingAsync(reading);
        await firstScope.ServiceProvider.GetRequiredService<IPrivacyRepository>().DeleteReadingsAsync(user.Id);

        await second.Invoking(s => s.AddReadingAsync(
            new Reading { UserId = user.Id, SpreadType = SpreadType.ThreeCard, Question = "again" }))
            .Should().ThrowAsync<SubscriptionRequiredException>();
        await second.Invoking(s => s.AddReadingAsync(
            new Reading { UserId = user.Id, SpreadType = SpreadType.SingleCard, Question = "again" }))
            .Should().ThrowAsync<QuotaExceededException>();
    }

    private async Task<User> CreateUserAsync()
    {
        var user = new User { Email = $"reading-concurrency-{Guid.NewGuid():N}@example.com", PasswordHash = "unused" };
        using var scope = fixture.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IUserRepository>().AddAsync(user);
        return user;
    }
}
