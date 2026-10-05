using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Interfaces;
using FutureViewer.Infrastructure.Persistence;
using FutureViewer.Integration.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class ReadingsEndpointTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public ReadingsEndpointTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Post_reading_returns_cards_and_interpretation()
    {
        var client = await CreateAuthenticatedSubscribedClient();

        var response = await client.PostAsJsonAsync("/api/readings",
            new CreateReadingRequest { SpreadType = SpreadType.ThreeCard, Question = "What awaits me?" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<ReadingResult>();
        result.Should().NotBeNull();
        result!.Cards.Should().HaveCount(3);
        result.Interpretation.Should().StartWith("Stub interpretation");
    }

    [Fact]
    public async Task Post_reading_rejects_empty_question()
    {
        var client = await CreateAuthenticatedSubscribedClient();

        var response = await client.PostAsJsonAsync("/api/readings",
            new CreateReadingRequest { SpreadType = SpreadType.SingleCard, Question = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_reading_does_not_require_optional_personalization_fields()
    {
        var client = await CreateAuthenticatedSubscribedClient(clearProfile: true);

        var response = await client.PostAsJsonAsync("/api/readings",
            new CreateReadingRequest { SpreadType = SpreadType.SingleCard, Question = "What should I notice?" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Post_reading_requires_warning_acknowledgement_for_subscriber()
    {
        var client = await CreateAuthenticatedSubscribedClient();

        var response = await client.PostAsJsonAsync("/api/readings",
            new CreateReadingRequest { SpreadType = SpreadType.SingleCard, Question = "needs rewrite" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        body!["error"].ToString().Should().Be("question_warning_unacknowledged");
        body["suggestedQuestion"].ToString().Should().Contain("обратить внимание");
    }

    [Fact]
    public async Task Post_reading_allows_subscriber_after_warning_acknowledgement()
    {
        var client = await CreateAuthenticatedSubscribedClient();

        var response = await client.PostAsJsonAsync("/api/readings",
            new CreateReadingRequest
            {
                SpreadType = SpreadType.SingleCard,
                Question = "needs rewrite",
                QuestionWarningAcknowledged = true
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<ReadingResult>();
        result!.Question.Should().Be("needs rewrite");
    }

    [Fact]
    public async Task Post_stream_reading_requires_warning_acknowledgement_before_writing_stream()
    {
        var client = await CreateAuthenticatedSubscribedClient();

        var response = await client.PostAsJsonAsync("/api/readings/stream",
            new CreateReadingRequest { SpreadType = SpreadType.SingleCard, Question = "needs rewrite" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        body!["error"].ToString().Should().Be("question_warning_unacknowledged");
        body["suggestedQuestion"].ToString().Should().Contain("обратить внимание");
    }

    [Fact]
    public async Task Post_validate_question_returns_accepted_without_creating_reading()
    {
        var client = await CreateAuthenticatedSubscribedClient();

        var response = await client.PostAsJsonAsync("/api/readings/validate-question",
            new CreateReadingRequest { SpreadType = SpreadType.SingleCard, Question = "What should I notice?" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        body!["status"].ToString().Should().Be("accepted");
    }

    [Fact]
    public async Task Post_validate_question_returns_subscriber_warning_payload()
    {
        var client = await CreateAuthenticatedSubscribedClient();

        var response = await client.PostAsJsonAsync("/api/readings/validate-question",
            new CreateReadingRequest { SpreadType = SpreadType.SingleCard, Question = "needs rewrite" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        body!["status"].ToString().Should().Be("needs_rewrite");
        body["canContinue"].ToString().Should().Be("True");
        body["requiresSubscription"].ToString().Should().Be("False");
        body["suggestedQuestion"].ToString().Should().Contain("обратить внимание");
    }

    [Fact]
    public async Task Post_validate_question_blocks_rejected_question_without_offering_paid_bypass()
    {
        var client = await CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/readings/validate-question",
            new CreateReadingRequest { SpreadType = SpreadType.SingleCard, Question = "rejected" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        body!["status"].ToString().Should().Be("rejected");
        body["canContinue"].ToString().Should().Be("False");
        body["requiresSubscription"].ToString().Should().Be("False");
        body["suggestedQuestion"].Should().BeNull();
        body["blockCode"].ToString().Should().Be("rejected");
    }

    [Fact]
    public async Task Post_reading_blocks_rejected_question_for_free_user()
    {
        var client = await CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/readings",
            new CreateReadingRequest { SpreadType = SpreadType.SingleCard, Question = "rejected" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        body!["error"].ToString().Should().Be("ai_privacy_blocked");
    }

    [Fact]
    public async Task Post_reading_without_token_returns_unauthorized()
    {
        var client = _fixture.CreateClient();

        var response = await client.PostAsJsonAsync("/api/readings",
            new CreateReadingRequest { SpreadType = SpreadType.SingleCard, Question = "q" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_reading_persists_requested_deck_type()
    {
        var client = await CreateAuthenticatedSubscribedClient();

        var response = await client.PostAsJsonAsync("/api/readings",
            new CreateReadingRequest
            {
                SpreadType = SpreadType.SingleCard,
                Question = "q",
                DeckType = DeckType.Thoth
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<ReadingResult>();
        result!.DeckType.Should().Be(DeckType.Thoth);
    }

    [Fact]
    public async Task Get_reading_by_id_returns_reading()
    {
        var client = await CreateAuthenticatedSubscribedClient();
        (await client.PutAsJsonAsync("/api/privacy/settings/history", new UpdateHistorySettingRequest { Enabled = true }))
            .EnsureSuccessStatusCode();
        var createResponse = await client.PostAsJsonAsync("/api/readings",
            new CreateReadingRequest { SpreadType = SpreadType.SingleCard, Question = "q", SaveToHistory = true });
        var created = await createResponse.Content.ReadFromJsonAsync<ReadingResult>();

        var getResponse = await client.GetAsync($"/api/readings/{created!.Id}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await getResponse.Content.ReadFromJsonAsync<ReadingResult>();
        fetched!.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task Get_reading_by_id_without_token_returns_unauthorized()
    {
        var anon = _fixture.CreateClient();
        var response = await anon.GetAsync($"/api/readings/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_reading_by_id_of_other_user_returns_not_found()
    {
        var owner = await CreateAuthenticatedSubscribedClient();
        (await owner.PutAsJsonAsync("/api/privacy/settings/history", new UpdateHistorySettingRequest { Enabled = true }))
            .EnsureSuccessStatusCode();
        var createResponse = await owner.PostAsJsonAsync("/api/readings",
            new CreateReadingRequest { SpreadType = SpreadType.SingleCard, Question = "private", SaveToHistory = true });
        var created = await createResponse.Content.ReadFromJsonAsync<ReadingResult>();

        var intruder = await CreateAuthenticatedSubscribedClient();
        var response = await intruder.GetAsync($"/api/readings/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("/api/readings", true, null)]
    [InlineData("/api/readings/stream", true, null)]
    [InlineData("/api/readings", true, false)]
    [InlineData("/api/readings/stream", true, false)]
    [InlineData("/api/readings", false, null)]
    [InlineData("/api/readings/stream", false, null)]
    [InlineData("/api/readings", false, true)]
    [InlineData("/api/readings/stream", false, true)]
    public async Task History_saves_by_default_and_honors_account_and_reading_opt_outs(
        string endpoint, bool accountHistoryEnabled, bool? saveToHistory)
    {
        var client = await CreateAuthenticatedSubscribedClient();
        if (!accountHistoryEnabled)
        {
            (await client.PutAsJsonAsync("/api/privacy/settings/history", new UpdateHistorySettingRequest { Enabled = false }))
                .EnsureSuccessStatusCode();
        }

        // Send a missing JSON property to exercise the API default, not only the DTO initializer.
        const string question = "What can I reflect on today?";
        var request = new Dictionary<string, object>
        {
            ["spreadType"] = SpreadType.SingleCard,
            ["question"] = question
        };
        if (saveToHistory.HasValue) request["saveToHistory"] = saveToHistory.Value;
        var response = await client.PostAsJsonAsync(endpoint, request);
        response.EnsureSuccessStatusCode();

        Guid readingId;
        if (endpoint.EndsWith("/stream"))
        {
            var frames = (await response.Content.ReadAsStringAsync()).Split('\n', StringSplitOptions.RemoveEmptyEntries);
            using var cards = JsonDocument.Parse(frames[0]);
            readingId = cards.RootElement.GetProperty("reading").GetProperty("id").GetGuid();
            using var done = JsonDocument.Parse(frames[^1]);
            done.RootElement.GetProperty("type").GetString().Should().Be("done");
        }
        else
        {
            var created = await response.Content.ReadFromJsonAsync<ReadingResult>();
            created!.Question.Should().Be(question);
            created.Interpretation.Should().StartWith("Stub interpretation");
            readingId = created.Id;
        }

        var shouldSave = accountHistoryEnabled && saveToHistory != false;
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.Readings.SingleAsync(x => x.Id == readingId);
        stored.SavedToHistory.Should().Be(shouldSave);
        stored.Question.Should().Be(shouldSave ? question : string.Empty);
        if (shouldSave) stored.AiInterpretation.Should().StartWith("Stub interpretation");
        else stored.AiInterpretation.Should().BeNull();
        var history = await (await client.GetAsync("/api/readings/history"))
            .Content.ReadFromJsonAsync<List<ReadingResult>>();
        history.Should().HaveCount(shouldSave ? 1 : 0);
        (await client.GetAsync($"/api/readings/{readingId}")).StatusCode
            .Should().Be(shouldSave ? HttpStatusCode.OK : HttpStatusCode.NotFound);
    }

    private async Task<HttpClient> CreateAuthenticatedSubscribedClient(bool clearProfile = false)
    {
        var (client, auth) = await CreateAuthenticatedClientWithAuth();

        using var scope = _fixture.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var user = await users.GetByIdAsync(auth.UserId);
        user!.SubscriptionStatus = SubscriptionStatus.Active;
        user.SubscriptionExpiresAt = DateTime.UtcNow.AddDays(30);
        if (clearProfile)
        {
            user.FirstName = null;
            user.LastName = null;
            user.BirthYear = null;
        }
        await users.UpdateAsync(user);

        return client;
    }

    private async Task<HttpClient> CreateAuthenticatedClient()
    {
        var (client, _) = await CreateAuthenticatedClientWithAuth();
        return client;
    }

    private async Task<(HttpClient Client, AuthResponse Auth)> CreateAuthenticatedClientWithAuth()
    {
        var client = _fixture.CreateClient();
        var email = $"reader-{Guid.NewGuid():N}@example.com";

        var auth = await _fixture.RegisterAndLoginAsync(client, email, "password123");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return (client, auth);
    }
}
