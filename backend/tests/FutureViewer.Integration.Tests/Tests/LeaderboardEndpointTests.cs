using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.Infrastructure.Persistence;
using FutureViewer.Integration.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class LeaderboardEndpointTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public LeaderboardEndpointTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Monthly_leaderboard_is_anonymous_and_returns_ok()
    {
        var client = _fixture.CreateClient();
        var response = await client.GetAsync("/api/leaderboard/monthly");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Alltime_leaderboard_is_anonymous_and_returns_ok()
    {
        var client = _fixture.CreateClient();
        var response = await client.GetAsync("/api/leaderboard/alltime");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Leaderboard_me_requires_authentication()
    {
        var client = _fixture.CreateClient();
        var response = await client.GetAsync("/api/leaderboard/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Leaderboard_me_returns_zero_summary_for_new_user()
    {
        var client = _fixture.CreateClient();
        var email = $"lb-{Guid.NewGuid():N}@example.com";

        var auth = await _fixture.RegisterAndLoginAsync(client, email, "password123");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var response = await client.GetAsync("/api/leaderboard/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await response.Content.ReadFromJsonAsync<UserScoreSummaryDto>();
        summary.Should().NotBeNull();
        summary!.TotalScore.Should().Be(0);
        summary.FeedbackScore.Should().Be(0);
        summary.AchievementScore.Should().Be(0);
        summary.FeedbackCount.Should().Be(0);
    }

    [Fact]
    public async Task Alltime_leaderboard_includes_user_with_only_achievement_points()
    {
        var client = _fixture.CreateClient();
        var email = $"lb-ach-{Guid.NewGuid():N}@example.com";

        var auth = await _fixture.RegisterAndLoginAsync(client, email, "password123");
        var userId = auth!.UserId;

        await GrantAchievementAsync(userId, "first_reading", DateTime.UtcNow);

        var response = await client.GetAsync("/api/leaderboard/alltime?take=200");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await response.Content.ReadFromJsonAsync<List<LeaderboardEntryDto>>();
        list.Should().NotBeNull();
        var entry = list!.FirstOrDefault(e => e.UserId == userId);
        entry.Should().NotBeNull();
        entry!.FeedbackScore.Should().Be(0);
        entry.AchievementScore.Should().BeGreaterThan(0);
        entry.TotalScore.Should().Be(entry.FeedbackScore + entry.AchievementScore);
        entry.FeedbackCount.Should().Be(0);
    }

    [Fact]
    public async Task Monthly_leaderboard_counts_achievements_unlocked_in_that_month_only()
    {
        var client = _fixture.CreateClient();
        var email = $"lb-ach-month-{Guid.NewGuid():N}@example.com";

        var auth = await _fixture.RegisterAndLoginAsync(client, email, "password123");
        var userId = auth!.UserId;

        // Grant one in current month, one in previous month
        var now = DateTime.UtcNow;
        await GrantAchievementAsync(userId, "first_reading", now);
        await GrantAchievementAsync(userId, "first_feedback", now.AddMonths(-2));

        var response = await client.GetAsync($"/api/leaderboard/monthly?year={now.Year}&month={now.Month}&take=200");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await response.Content.ReadFromJsonAsync<List<LeaderboardEntryDto>>();
        list.Should().NotBeNull();
        var entry = list!.FirstOrDefault(e => e.UserId == userId);
        entry.Should().NotBeNull();
        entry!.AchievementScore.Should().Be(10); // only first_reading (10pts), not first_feedback
    }

    [Fact]
    public async Task Leaderboard_me_returns_split_feedback_and_achievement_scores()
    {
        var client = _fixture.CreateClient();
        var email = $"lb-me-{Guid.NewGuid():N}@example.com";

        var auth = await _fixture.RegisterAndLoginAsync(client, email, "password123");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        await GrantAchievementAsync(auth.UserId, "first_reading", DateTime.UtcNow);

        var response = await client.GetAsync("/api/leaderboard/me");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await response.Content.ReadFromJsonAsync<UserScoreSummaryDto>();
        summary.Should().NotBeNull();
        summary!.AchievementScore.Should().Be(10);
        summary.FeedbackScore.Should().Be(0);
        summary.TotalScore.Should().Be(10);
    }

    private async Task GrantAchievementAsync(Guid userId, string code, DateTime unlockedAt)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var achievement = await db.Achievements.AsNoTracking().FirstAsync(a => a.Code == code);
        db.UserAchievements.Add(new UserAchievement
        {
            UserId = userId,
            AchievementId = achievement.Id,
            UnlockedAt = unlockedAt
        });
        await db.SaveChangesAsync();
    }
}
