using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.Integration.Tests.Fixtures;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class CardsEndpointTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public CardsEndpointTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Get_glossary_returns_all_78_cards_with_variants()
    {
        var client = _fixture.CreateClient();

        var response = await client.GetAsync("/api/cards/glossary");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var glossary = await response.Content.ReadFromJsonAsync<List<CardGlossaryDto>>();
        glossary.Should().NotBeNull();
        glossary!.Should().HaveCount(78);
        glossary.Should().OnlyContain(c => !string.IsNullOrWhiteSpace(c.NameEn));
        glossary.Should().OnlyContain(c => c.UprightKeywords.Count > 0);
        glossary.Should().OnlyContain(c => c.DeckVariants.Count == 5);
    }

    [Fact]
    public async Task Get_card_by_id_returns_card_details()
    {
        var client = _fixture.CreateClient();

        var response = await client.GetAsync("/api/cards/1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var card = await response.Content.ReadFromJsonAsync<CardGlossaryDto>();
        card.Should().NotBeNull();
        card!.Id.Should().Be(1);
        card.DeckVariants.Should().HaveCount(5);
        card.UprightKeywords.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Get_card_by_missing_id_returns_not_found()
    {
        var client = _fixture.CreateClient();

        var response = await client.GetAsync("/api/cards/9999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
