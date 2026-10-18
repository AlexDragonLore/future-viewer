using System.Data.Common;
using FluentAssertions;
using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.Services;
using FutureViewer.Infrastructure.Persistence;
using FutureViewer.Infrastructure.Persistence.Repositories;
using FutureViewer.Integration.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class AchievementPerformanceTests(IntegrationTestFixture fixture) : IClassFixture<IntegrationTestFixture>
{
    [Fact]
    public async Task Combined_status_reads_catalog_and_owned_achievements_once_and_uses_five_selects()
    {
        using var scope = fixture.Services.CreateScope();
        var original = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = NewUser();
        var first = await original.Achievements.SingleAsync(a => a.Code == AchievementService.Codes.FirstReading);
        original.Users.Add(user);
        original.Readings.Add(new Reading { SpreadType = SpreadType.SingleCard, UserId = user.Id, Question = "fixture", SavedToHistory = true });
        original.UserAchievements.Add(new UserAchievement { UserId = user.Id, AchievementId = first.Id });
        await original.SaveChangesAsync();
        var capture = new QueryCapture();
        await using var db = CreateDb(original, capture);
        var service = Service(db);
        var status = await service.CheckAndGetAllAsync(user.Id);
        status.Should().HaveCount(7);
        status.Single(a => a.Code == AchievementService.Codes.FirstReading).UnlockedAt.Should().NotBeNull();
        capture.Commands.Should().HaveCount(5);
        capture.Commands.Count(sql => sql.Contains("FROM achievements AS")).Should().Be(1);
        capture.Commands.Count(sql => sql.Contains("FROM user_achievements AS")).Should().Be(1);
        capture.Commands.Should().OnlyContain(sql => !sql.Contains("self_report"));
    }

    [Fact]
    public async Task Concurrent_batch_grants_are_unique_idempotent_and_both_status_responses_see_unlocks()
    {
        using var scope = fixture.Services.CreateScope();
        var original = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = NewUser();
        original.Users.Add(user);
        original.Readings.AddRange(Enumerable.Range(0, 10).Select(_ =>
            new Reading { SpreadType = SpreadType.SingleCard, UserId = user.Id, Question = "fixture", SavedToHistory = true }));
        await original.SaveChangesAsync();
        await using var firstDb = CreateDb(original);
        await using var secondDb = CreateDb(original);
        var responses = await Task.WhenAll(Service(firstDb).CheckAndGetAllAsync(user.Id), Service(secondDb).CheckAndGetAllAsync(user.Id));
        foreach (var response in responses)
            response.Where(a => a.UnlockedAt != null).Select(a => a.Code)
                .Should().BeEquivalentTo("first_reading", "total_10");
        (await original.UserAchievements.CountAsync(a => a.UserId == user.Id)).Should().Be(2);
        (await Service(firstDb).CheckAndGrantAsync(user.Id)).Should().BeEmpty();

        var ids = await original.Achievements.Where(a => a.Code == "streak_3" || a.Code == "streak_7")
            .Select(a => a.Id).ToArrayAsync();
        UserAchievement[] Grants() => ids.Select(id => new UserAchievement { UserId = user.Id, AchievementId = id }).ToArray();
        var grants = await Task.WhenAll(new AchievementRepository(firstDb).GrantManyAsync(Grants()),
            new AchievementRepository(secondDb).GrantManyAsync(Grants()));
        grants.Sum(x => x.Count).Should().Be(2);
        (await original.UserAchievements.CountAsync(a => a.UserId == user.Id)).Should().Be(4);
    }

    private static AchievementService Service(AppDbContext db) => new(
        new AchievementRepository(db), new ReadingRepository(db), new UserRepository(db));
    private static User NewUser() => new() { Email = $"achievement-perf-{Guid.NewGuid():N}@example.com", PasswordHash = "unused" };
    private static AppDbContext CreateDb(AppDbContext original, QueryCapture? capture = null)
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(original.Database.GetConnectionString());
        if (capture != null) builder.AddInterceptors(capture);
        return new AppDbContext(builder.Options);
    }
    private sealed class QueryCapture : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];
        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }
}
