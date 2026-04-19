using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.DTOs.Admin;
using FutureViewer.DomainServices.Interfaces;
using FutureViewer.Integration.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class AdminFeedbackEndpointTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public AdminFeedbackEndpointTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Admin_feedbacks_endpoint_requires_admin_role()
    {
        var (client, _) = await CreateAuthenticatedClient(asAdmin: false);

        var response = await client.GetAsync("/api/admin/feedbacks");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_feedbacks_list_returns_paged_results()
    {
        var (client, _) = await CreateAuthenticatedClient(asAdmin: true);

        var response = await client.GetAsync("/api/admin/feedbacks?page=1&pageSize=5");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<AdminFeedbackList>();
        payload.Should().NotBeNull();
        payload!.Items.Should().NotBeNull();
        payload.Total.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Admin_can_create_synthetic_feedback_and_score_grants_achievement()
    {
        var (adminClient, _) = await CreateAuthenticatedClient(asAdmin: true);
        var (userClient, userAuth) = await CreateAuthenticatedSubscribedClient();
        var readingId = await CreateReading(userClient);

        // remove the auto-scheduled feedback so synthetic creation can proceed cleanly
        await DeleteAutoFeedback(readingId);

        var response = await adminClient.PostAsJsonAsync("/api/admin/feedbacks/synthetic", new
        {
            readingId,
            aiScore = 10,
            aiScoreReason = "Excellent",
            isSincere = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await response.Content.ReadFromJsonAsync<AdminFeedbackDto>();
        dto.Should().NotBeNull();
        dto!.AiScore.Should().Be(10);
        dto.Status.Should().Be(FeedbackStatus.Scored);
        dto.UserId.Should().Be(userAuth.UserId);

        // Achievement check: a perfect_10 should now exist for this user
        userClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userAuth.AccessToken);
        var achievements = await userClient.GetFromJsonAsync<List<AchievementDto>>("/api/achievements/me");
        achievements.Should().NotBeNull();
        achievements!.Any(a => a.Code == "perfect_10" && a.UnlockedAt != null).Should().BeTrue();
    }

    [Fact]
    public async Task Admin_can_update_feedback_score_and_status()
    {
        var (adminClient, _) = await CreateAuthenticatedClient(asAdmin: true);
        var (userClient, _) = await CreateAuthenticatedSubscribedClient();
        var readingId = await CreateReading(userClient);

        var feedback = await GetFeedbackByReading(readingId);
        feedback.Should().NotBeNull();

        var update = await adminClient.PutAsJsonAsync($"/api/admin/feedbacks/{feedback!.Id}", new
        {
            aiScore = 7,
            status = FeedbackStatus.Scored,
            isSincere = true
        });

        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await update.Content.ReadFromJsonAsync<AdminFeedbackDto>();
        dto!.AiScore.Should().Be(7);
        dto.Status.Should().Be(FeedbackStatus.Scored);
    }

    [Fact]
    public async Task Admin_can_delete_feedback()
    {
        var (adminClient, _) = await CreateAuthenticatedClient(asAdmin: true);
        var (userClient, _) = await CreateAuthenticatedSubscribedClient();
        var readingId = await CreateReading(userClient);
        var feedback = await GetFeedbackByReading(readingId);

        var del = await adminClient.DeleteAsync($"/api/admin/feedbacks/{feedback!.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterDelete = await GetFeedbackByReading(readingId);
        afterDelete.Should().BeNull();
    }

    [Fact]
    public async Task Admin_create_scheduled_feedback_with_replace_overwrites_existing()
    {
        var (adminClient, _) = await CreateAuthenticatedClient(asAdmin: true);
        var (userClient, _) = await CreateAuthenticatedSubscribedClient();
        var readingId = await CreateReading(userClient);

        var conflict = await adminClient.PostAsJsonAsync("/api/admin/feedbacks", new
        {
            readingId,
            bypassDelay = true
        });
        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var replaced = await adminClient.PostAsJsonAsync("/api/admin/feedbacks", new
        {
            readingId,
            bypassDelay = true,
            replace = true
        });
        replaced.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await replaced.Content.ReadFromJsonAsync<AdminFeedbackDto>();
        dto!.Status.Should().Be(FeedbackStatus.Pending);
        dto.ScheduledAt.Should().BeBefore(DateTime.UtcNow);
    }

    [Fact]
    public async Task Admin_run_notifications_returns_processed_count_zero_when_no_telegram()
    {
        var (adminClient, _) = await CreateAuthenticatedClient(asAdmin: true);

        var response = await adminClient.PostAsync("/api/admin/feedbacks/run-notifications", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<RunNotificationsResult>();
        payload!.Processed.Should().Be(0);
    }

    private async Task<Guid> CreateReading(HttpClient client)
    {
        var created = await client.PostAsJsonAsync("/api/readings",
            new CreateReadingRequest { SpreadType = SpreadType.SingleCard, Question = "What should I focus on?" });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var reading = await created.Content.ReadFromJsonAsync<ReadingResult>();
        return reading!.Id;
    }

    private async Task<ReadingFeedbackSnapshot?> GetFeedbackByReading(Guid readingId)
    {
        using var scope = _fixture.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IFeedbackRepository>();
        var fb = await repo.GetByReadingIdAsync(readingId);
        return fb is null ? null : new ReadingFeedbackSnapshot { Id = fb.Id, UserId = fb.UserId };
    }

    private async Task DeleteAutoFeedback(Guid readingId)
    {
        using var scope = _fixture.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IFeedbackRepository>();
        var fb = await repo.GetByReadingIdAsync(readingId);
        if (fb is not null)
            await repo.DeleteAsync(fb.Id);
    }

    private async Task<(HttpClient Client, AuthResponse Auth)> CreateAuthenticatedClient(bool asAdmin)
    {
        var client = _fixture.CreateClient();
        var email = $"admin-test-{Guid.NewGuid():N}@example.com";

        var register = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Email = email, Password = "password123" });
        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>();
        auth.Should().NotBeNull();

        if (asAdmin)
        {
            using var scope = _fixture.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var user = await users.GetByIdAsync(auth!.UserId);
            user!.IsAdmin = true;
            await users.UpdateAsync(user);

            // re-issue token after IsAdmin flip
            var login = await client.PostAsJsonAsync("/api/auth/login",
                new LoginRequest { Email = email, Password = "password123" });
            auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return (client, auth);
    }

    private async Task<(HttpClient Client, AuthResponse Auth)> CreateAuthenticatedSubscribedClient()
    {
        var client = _fixture.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@example.com";

        var register = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Email = email, Password = "password123" });
        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        using var scope = _fixture.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var user = await users.GetByIdAsync(auth.UserId);
        user!.SubscriptionStatus = SubscriptionStatus.Active;
        user.SubscriptionExpiresAt = DateTime.UtcNow.AddDays(30);
        await users.UpdateAsync(user);

        return (client, auth);
    }

    private sealed class ReadingFeedbackSnapshot
    {
        public Guid Id { get; init; }
        public Guid UserId { get; init; }
    }

    private sealed class AdminFeedbackList
    {
        public List<AdminFeedbackDto> Items { get; init; } = new();
        public int Total { get; init; }
    }

    private sealed class RunNotificationsResult
    {
        public int Processed { get; init; }
    }
}
