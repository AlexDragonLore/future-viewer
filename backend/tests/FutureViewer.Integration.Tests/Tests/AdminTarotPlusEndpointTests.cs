using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.DTOs.Admin;
using FutureViewer.DomainServices.Interfaces;
using FutureViewer.Integration.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class AdminTarotPlusEndpointTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public AdminTarotPlusEndpointTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Admin_tarot_plus_list_requires_admin_role()
    {
        var (client, _) = await CreateAuthenticatedClient(asAdmin: false);

        var response = await client.GetAsync("/api/admin/tarot-plus");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_tarot_plus_list_returns_sessions_for_user()
    {
        var (adminClient, _) = await CreateAuthenticatedClient(asAdmin: true);
        var (userClient, user) = await CreateAuthenticatedClient(asAdmin: false);
        var preview = await userClient.PostAsJsonAsync("/api/tarot-plus/preview", PreviewRequest());
        preview.StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await adminClient.GetAsync($"/api/admin/tarot-plus?userId={user.UserId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<AdminTarotPlusList>();
        payload.Should().NotBeNull();
        payload!.Items.Should().Contain(x => x.UserId == user.UserId && x.UserEmail == user.Email);
        payload.Total.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Admin_can_set_tarot_plus_credits()
    {
        var (adminClient, _) = await CreateAuthenticatedClient(asAdmin: true);
        var (_, target) = await CreateAuthenticatedClient(asAdmin: false);

        var response = await adminClient.PutAsJsonAsync(
            $"/api/admin/users/{target.UserId}/tarot-plus-credits",
            new { credits = 2 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await response.Content.ReadFromJsonAsync<AdminUserDetailDto>();
        detail.Should().NotBeNull();
        detail!.TarotPlusCredits.Should().Be(2);
    }

    private static CreateTarotPlusPreviewRequest PreviewRequest() => new()
    {
        CoreRequest = "Хочу понять следующий жизненный шаг",
        MainSphere = "работа",
        DesiredOutcome = "получить план"
    };

    private async Task<(HttpClient Client, AuthResponse Auth)> CreateAuthenticatedClient(bool asAdmin)
    {
        var client = _fixture.CreateClient();
        var email = $"admin-tarot-plus-{Guid.NewGuid():N}@example.com";
        var auth = await _fixture.RegisterAndLoginAsync(client, email, "password123");

        if (asAdmin)
        {
            using var scope = _fixture.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var user = await users.GetByIdAsync(auth!.UserId);
            user!.IsAdmin = true;
            await users.UpdateAsync(user);

            var login = await client.PostAsJsonAsync("/api/auth/login",
                new LoginRequest { Email = email, Password = "password123" });
            auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return (client, auth);
    }

    private sealed class AdminTarotPlusList
    {
        public List<AdminTarotPlusSessionDto> Items { get; init; } = new();
        public int Total { get; init; }
    }
}
