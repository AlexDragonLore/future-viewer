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

public sealed class ProfileEndpointTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public ProfileEndpointTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Get_and_update_personalization_returns_profile()
    {
        var client = await CreateAuthenticatedClient();

        var update = await client.PutAsJsonAsync("/api/profile/personalization",
            new UpdatePersonalizationRequest
            {
                FirstName = "Ada",
                LastName = "Lovelace",
                BirthDate = new DateOnly(1815, 12, 10)
            });

        update.StatusCode.Should().Be(HttpStatusCode.OK);

        var get = await client.GetAsync("/api/profile/personalization");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await get.Content.ReadFromJsonAsync<PersonalizationDto>();
        profile.Should().NotBeNull();
        profile!.FirstName.Should().Be("Ada");
        profile.LastName.Should().Be("Lovelace");
        profile.BirthDate.Should().Be(new DateOnly(1815, 12, 10));
        profile.IsComplete.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_memory_rule_and_clear_memory_update_profile()
    {
        var client = await CreateAuthenticatedClient();

        var createReading = await client.PostAsJsonAsync("/api/readings",
            new CreateReadingRequest { SpreadType = SpreadType.SingleCard, Question = "remember me" });
        createReading.StatusCode.Should().Be(HttpStatusCode.Created);

        var profile = await (await client.GetAsync("/api/profile/personalization"))
            .Content.ReadFromJsonAsync<PersonalizationDto>();
        profile!.MemoryRules.Should().HaveCount(1);

        var deleteOne = await client.DeleteAsync($"/api/profile/personalization/memory/{profile.MemoryRules[0].Id}");
        deleteOne.StatusCode.Should().Be(HttpStatusCode.NoContent);
        profile = await (await client.GetAsync("/api/profile/personalization"))
            .Content.ReadFromJsonAsync<PersonalizationDto>();
        profile!.MemoryRules.Should().BeEmpty();

        createReading = await client.PostAsJsonAsync("/api/readings",
            new CreateReadingRequest { SpreadType = SpreadType.SingleCard, Question = "remember me" });
        createReading.StatusCode.Should().Be(HttpStatusCode.Created);

        var clear = await client.DeleteAsync("/api/profile/personalization/memory");
        clear.StatusCode.Should().Be(HttpStatusCode.NoContent);
        profile = await (await client.GetAsync("/api/profile/personalization"))
            .Content.ReadFromJsonAsync<PersonalizationDto>();
        profile!.MemoryRules.Should().BeEmpty();
    }

    private async Task<HttpClient> CreateAuthenticatedClient()
    {
        var client = _fixture.CreateClient();
        var auth = await _fixture.RegisterAndLoginAsync(client, $"profile-{Guid.NewGuid():N}@example.com", "password123");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        using var scope = _fixture.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var user = await users.GetByIdAsync(auth.UserId);
        user!.SubscriptionStatus = SubscriptionStatus.Active;
        user.SubscriptionExpiresAt = DateTime.UtcNow.AddDays(30);
        await users.UpdateAsync(user);

        return client;
    }
}
