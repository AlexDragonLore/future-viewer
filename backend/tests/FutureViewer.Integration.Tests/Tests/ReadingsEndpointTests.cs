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
        var createResponse = await client.PostAsJsonAsync("/api/readings",
            new CreateReadingRequest { SpreadType = SpreadType.SingleCard, Question = "q" });
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
        var createResponse = await owner.PostAsJsonAsync("/api/readings",
            new CreateReadingRequest { SpreadType = SpreadType.SingleCard, Question = "private" });
        var created = await createResponse.Content.ReadFromJsonAsync<ReadingResult>();

        var intruder = await CreateAuthenticatedSubscribedClient();
        var response = await intruder.GetAsync($"/api/readings/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<HttpClient> CreateAuthenticatedSubscribedClient()
    {
        var client = _fixture.CreateClient();
        var email = $"reader-{Guid.NewGuid():N}@example.com";

        var auth = await _fixture.RegisterAndLoginAsync(client, email, "password123");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        using var scope = _fixture.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var user = await users.GetByIdAsync(auth.UserId);
        user!.SubscriptionStatus = SubscriptionStatus.Active;
        user.SubscriptionExpiresAt = DateTime.UtcNow.AddDays(30);
        await users.UpdateAsync(user);

        return client;
    }
}
