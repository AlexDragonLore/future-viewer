using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.Integration.Tests.Fixtures;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class TarotPlusEndpointTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public TarotPlusEndpointTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Preview_requires_auth()
    {
        var client = _fixture.CreateClient();

        var response = await client.PostAsJsonAsync("/api/tarot-plus/preview", PreviewRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Preview_creates_preview_session()
    {
        var client = await AuthenticatedClientAsync("tp-preview");

        var response = await client.PostAsJsonAsync("/api/tarot-plus/preview", PreviewRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var preview = await response.Content.ReadFromJsonAsync<TarotPlusPreviewDto>();
        preview.Should().NotBeNull();
        preview!.Session.PriceRub.Should().Be(100m);
        preview.Session.PreviewText.Should().Be("Stub Tarot+ preview");
    }

    [Fact]
    public async Task Payment_returns_confirmation_url()
    {
        var client = await AuthenticatedClientAsync("tp-pay");
        var preview = await CreatePreviewAsync(client);

        var response = await client.PostAsync($"/api/tarot-plus/{preview.Session.Id}/payment", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payment = await response.Content.ReadFromJsonAsync<PaymentCreationDto>();
        payment.Should().NotBeNull();
        payment!.ConfirmationUrl.Should().StartWith("https://pay.example/confirm/");
    }

    [Fact]
    public async Task Answers_reject_unpaid_session()
    {
        var client = await AuthenticatedClientAsync("tp-unpaid");
        var preview = await CreatePreviewAsync(client);

        var response = await client.PostAsJsonAsync($"/api/tarot-plus/{preview.Session.Id}/answers", new AddTarotPlusAnswerRequest
        {
            Question = "Что важно?",
            Answer = "Ответ"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task History_returns_only_own_sessions()
    {
        var firstClient = await AuthenticatedClientAsync("tp-history-a");
        var secondClient = await AuthenticatedClientAsync("tp-history-b");
        var firstPreview = await CreatePreviewAsync(firstClient);
        await CreatePreviewAsync(secondClient);

        var response = await firstClient.GetAsync("/api/tarot-plus/history");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var history = await response.Content.ReadFromJsonAsync<List<TarotPlusSessionListItemDto>>();
        history.Should().NotBeNull();
        history!.Should().ContainSingle(x => x.Id == firstPreview.Session.Id);
    }

    private async Task<HttpClient> AuthenticatedClientAsync(string emailPrefix)
    {
        var client = _fixture.CreateClient();
        var auth = await _fixture.RegisterAndLoginAsync(
            client,
            $"{emailPrefix}-{Guid.NewGuid():N}@example.com",
            "password123");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private static CreateTarotPlusPreviewRequest PreviewRequest() => new()
    {
        CoreRequest = "Хочу понять следующий жизненный шаг",
        MainSphere = "работа",
        DesiredOutcome = "получить ясный план"
    };

    private static async Task<TarotPlusPreviewDto> CreatePreviewAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/tarot-plus/preview", PreviewRequest());
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TarotPlusPreviewDto>()
               ?? throw new InvalidOperationException("Preview returned null");
    }
}
