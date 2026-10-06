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

public sealed class HistoryEndpointTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public HistoryEndpointTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task History_returns_user_readings()
    {
        var client = _fixture.CreateClient();
        var email = $"hist-{Guid.NewGuid():N}@example.com";

        var auth = await _fixture.RegisterAndLoginAsync(client, email, "password123");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        using (var scope = _fixture.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var user = await users.GetByIdAsync(auth.UserId);
            user!.SubscriptionStatus = SubscriptionStatus.Active;
            user.SubscriptionExpiresAt = DateTime.UtcNow.AddDays(30);
            user.HistoryEnabled = true;
            await users.UpdateAsync(user);
        }

        await client.PostAsJsonAsync("/api/readings",
            new CreateReadingRequest { SpreadType = SpreadType.SingleCard, Question = "q1", SaveToHistory = true });
        await client.PostAsJsonAsync("/api/readings",
            new CreateReadingRequest { SpreadType = SpreadType.ThreeCard, Question = "q2", SaveToHistory = true });

        var response = await client.GetAsync("/api/readings/history");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var history = await response.Content.ReadFromJsonAsync<List<ReadingResult>>();
        history.Should().NotBeNull();
        history!.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Delete_reading_hides_it_from_history_and_detail()
    {
        var client = await CreateSubscribedClient();
        var createResponse = await client.PostAsJsonAsync("/api/readings",
            new CreateReadingRequest { SpreadType = SpreadType.SingleCard, Question = "delete me", SaveToHistory = true });
        var created = await createResponse.Content.ReadFromJsonAsync<ReadingResult>();

        var deleteResponse = await client.DeleteAsync($"/api/readings/{created!.Id}");
        var historyResponse = await client.GetAsync("/api/readings/history");
        var detailResponse = await client.GetAsync($"/api/readings/{created.Id}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        detailResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var history = await historyResponse.Content.ReadFromJsonAsync<List<ReadingResult>>();
        history!.Select(r => r.Id).Should().NotContain(created.Id);

        using var scope = _fixture.Services.CreateScope();
        var readings = scope.ServiceProvider.GetRequiredService<IReadingRepository>();
        var stored = (await readings.GetByIdAsync(created.Id))!;
        stored.DeletedFromHistoryAt.Should().NotBeNull();
        stored.Question.Should().Be(created.Question);
        stored.AiInterpretation.Should().Be(created.Interpretation);
        (await readings.CountTodayByUserAsync(stored.UserId!.Value)).Should().Be(1);
        (await client.DeleteAsync($"/api/readings/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_reading_hides_associated_feedback_link()
    {
        var client = await CreateSubscribedClient();
        var createResponse = await client.PostAsJsonAsync("/api/readings",
            new CreateReadingRequest { SpreadType = SpreadType.SingleCard, Question = "feedback delete", SaveToHistory = true });
        var created = await createResponse.Content.ReadFromJsonAsync<ReadingResult>();

        string token;
        using (var scope = _fixture.Services.CreateScope())
        {
            var feedbacks = scope.ServiceProvider.GetRequiredService<IFeedbackRepository>();
            var feedback = await feedbacks.GetByReadingIdAsync(created!.Id);
            token = feedback!.Token;
        }

        await client.DeleteAsync($"/api/readings/{created!.Id}");
        var feedbackResponse = await client.GetAsync($"/api/feedbacks/{token}");

        feedbackResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task History_without_token_returns_unauthorized()
    {
        var client = _fixture.CreateClient();
        var response = await client.GetAsync("/api/readings/history");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<HttpClient> CreateSubscribedClient()
    {
        var client = _fixture.CreateClient();
        var email = $"hist-{Guid.NewGuid():N}@example.com";

        var auth = await _fixture.RegisterAndLoginAsync(client, email, "password123");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        using var scope = _fixture.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var user = await users.GetByIdAsync(auth.UserId);
        user!.SubscriptionStatus = SubscriptionStatus.Active;
        user.SubscriptionExpiresAt = DateTime.UtcNow.AddDays(30);
        user.HistoryEnabled = true;
        await users.UpdateAsync(user);

        return client;
    }
}
