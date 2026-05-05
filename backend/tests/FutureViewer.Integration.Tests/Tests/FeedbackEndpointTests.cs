using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Interfaces;
using FutureViewer.Integration.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class FeedbackEndpointTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public FeedbackEndpointTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Get_feedback_by_unknown_token_returns_not_found()
    {
        var client = _fixture.CreateClient();

        var response = await client.GetAsync("/api/feedbacks/does-not-exist");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Submit_feedback_rejects_short_selfreport()
    {
        var (client, auth) = await CreateAuthenticatedSubscribedClient();
        var (_, token) = await CreateReadingAndGetFeedback(client, auth);

        var response = await client.PostAsJsonAsync($"/api/feedbacks/{token}",
            new { SelfReport = "short" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_feedback_by_token_returns_question_and_status()
    {
        var (client, auth) = await CreateAuthenticatedSubscribedClient();
        var (readingId, token) = await CreateReadingAndGetFeedback(client, auth);

        var anon = _fixture.CreateClient();
        var response = await anon.GetAsync($"/api/feedbacks/{token}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<FeedbackDto>();
        dto.Should().NotBeNull();
        dto!.ReadingId.Should().Be(readingId);
        dto.Status.Should().Be(FeedbackStatus.Pending);
    }

    [Fact]
    public async Task Submit_feedback_scores_and_blocks_resubmit()
    {
        var (client, auth) = await CreateAuthenticatedSubscribedClient();
        var (_, token) = await CreateReadingAndGetFeedback(client, auth);

        var anon = _fixture.CreateClient();
        var submission = await anon.PostAsJsonAsync($"/api/feedbacks/{token}",
            new { SelfReport = "I followed the advice for three days carefully and reflected each evening." });

        submission.StatusCode.Should().Be(HttpStatusCode.OK);
        var scored = await submission.Content.ReadFromJsonAsync<FeedbackDto>();
        scored!.Status.Should().Be(FeedbackStatus.Scored);
        scored.AiScore.Should().NotBeNull();

        var resubmit = await anon.PostAsJsonAsync($"/api/feedbacks/{token}",
            new { SelfReport = "Trying to re-submit a different answer to cheat the score." });
        resubmit.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task My_feedbacks_requires_authentication()
    {
        var anon = _fixture.CreateClient();
        var response = await anon.GetAsync("/api/feedbacks/my");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task My_feedbacks_returns_user_feedbacks()
    {
        var (client, auth) = await CreateAuthenticatedSubscribedClient();
        await CreateReadingAndGetFeedback(client, auth);

        var response = await client.GetAsync("/api/feedbacks/my");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<List<FeedbackDto>>();
        list.Should().NotBeNull();
        list!.Should().HaveCountGreaterThanOrEqualTo(1);
    }

    private async Task<(Guid ReadingId, string Token)> CreateReadingAndGetFeedback(HttpClient client, AuthResponse auth)
    {
        var created = await client.PostAsJsonAsync("/api/readings",
            new CreateReadingRequest { SpreadType = SpreadType.SingleCard, Question = "What should I focus on today?" });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var reading = await created.Content.ReadFromJsonAsync<ReadingResult>();

        using var scope = _fixture.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IFeedbackRepository>();
        var feedback = await repo.GetByReadingIdAsync(reading!.Id);
        feedback.Should().NotBeNull();
        return (reading.Id, feedback!.Token);
    }

    private async Task<(HttpClient Client, AuthResponse Auth)> CreateAuthenticatedSubscribedClient()
    {
        var client = _fixture.CreateClient();
        var email = $"feedback-{Guid.NewGuid():N}@example.com";

        var auth = await _fixture.RegisterAndLoginAsync(client, email, "password123");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        using var scope = _fixture.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var user = await users.GetByIdAsync(auth.UserId);
        user!.SubscriptionStatus = SubscriptionStatus.Active;
        user.SubscriptionExpiresAt = DateTime.UtcNow.AddDays(30);
        await users.UpdateAsync(user);

        return (client, auth);
    }
}
