using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.Integration.Tests.Fixtures;

namespace FutureViewer.Integration.Tests.Tests;

public class ReadingsEndpointTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public ReadingsEndpointTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Post_reading_returns_cards_and_interpretation()
    {
        var client = _fixture.CreateClient();

        var response = await client.PostAsJsonAsync("/api/readings", new CreateReadingRequest(SpreadType.ThreeCard, "What awaits me?"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<ReadingResult>();
        result.Should().NotBeNull();
        result!.Cards.Should().HaveCount(3);
        result.Interpretation.Should().StartWith("Stub interpretation");
    }

    [Fact]
    public async Task Post_reading_rejects_empty_question()
    {
        var client = _fixture.CreateClient();

        var response = await client.PostAsJsonAsync("/api/readings", new CreateReadingRequest(SpreadType.SingleCard, ""));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_reading_by_id_returns_reading()
    {
        var client = _fixture.CreateClient();
        var createResponse = await client.PostAsJsonAsync("/api/readings", new CreateReadingRequest(SpreadType.SingleCard, "q"));
        var created = await createResponse.Content.ReadFromJsonAsync<ReadingResult>();

        var getResponse = await client.GetAsync($"/api/readings/{created!.Id}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await getResponse.Content.ReadFromJsonAsync<ReadingResult>();
        fetched!.Id.Should().Be(created.Id);
    }
}
