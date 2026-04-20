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

public sealed class AdminAchievementTelegramEndpointTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public AdminAchievementTelegramEndpointTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Grant_achievement_requires_admin_role()
    {
        var (client, _) = await CreateAuthenticatedClient(asAdmin: false);
        var (_, target) = await CreateUser($"ach-target-{Guid.NewGuid():N}@example.com");

        var response = await client.PostAsJsonAsync(
            $"/api/admin/users/{target.UserId}/achievements",
            new { code = "first_reading" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_can_grant_and_revoke_achievement_by_code()
    {
        var (adminClient, _) = await CreateAuthenticatedClient(asAdmin: true);
        var (_, target) = await CreateUser($"ach-grant-{Guid.NewGuid():N}@example.com");

        var grant = await adminClient.PostAsJsonAsync(
            $"/api/admin/users/{target.UserId}/achievements",
            new { code = "first_reading" });
        grant.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await grant.Content.ReadFromJsonAsync<AchievementDto>();
        dto!.Code.Should().Be("first_reading");
        dto.UnlockedAt.Should().NotBeNull();

        var afterGrant = await adminClient.GetAsync($"/api/admin/users/{target.UserId}");
        var detail = await afterGrant.Content.ReadFromJsonAsync<AdminUserDetailDto>();
        detail!.Achievements.Should().Contain(a => a.Code == "first_reading");

        var revoke = await adminClient.DeleteAsync(
            $"/api/admin/users/{target.UserId}/achievements/first_reading");
        revoke.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterRevoke = await adminClient.GetAsync($"/api/admin/users/{target.UserId}");
        var detail2 = await afterRevoke.Content.ReadFromJsonAsync<AdminUserDetailDto>();
        detail2!.Achievements.Should().NotContain(a => a.Code == "first_reading");
    }

    [Fact]
    public async Task Grant_achievement_returns_404_for_unknown_code()
    {
        var (adminClient, _) = await CreateAuthenticatedClient(asAdmin: true);
        var (_, target) = await CreateUser($"ach-unknown-{Guid.NewGuid():N}@example.com");

        var response = await adminClient.PostAsJsonAsync(
            $"/api/admin/users/{target.UserId}/achievements",
            new { code = "does_not_exist" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Grant_duplicate_achievement_returns_409()
    {
        var (adminClient, _) = await CreateAuthenticatedClient(asAdmin: true);
        var (_, target) = await CreateUser($"ach-dup-{Guid.NewGuid():N}@example.com");

        var first = await adminClient.PostAsJsonAsync(
            $"/api/admin/users/{target.UserId}/achievements",
            new { code = "first_reading" });
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var duplicate = await adminClient.PostAsJsonAsync(
            $"/api/admin/users/{target.UserId}/achievements",
            new { code = "first_reading" });

        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Revoke_nonexistent_achievement_returns_404()
    {
        var (adminClient, _) = await CreateAuthenticatedClient(asAdmin: true);
        var (_, target) = await CreateUser($"ach-revoke-missing-{Guid.NewGuid():N}@example.com");

        var response = await adminClient.DeleteAsync(
            $"/api/admin/users/{target.UserId}/achievements/first_reading");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Recheck_achievements_returns_ok_for_user_with_no_activity()
    {
        var (adminClient, _) = await CreateAuthenticatedClient(asAdmin: true);
        var (_, target) = await CreateUser($"ach-recheck-{Guid.NewGuid():N}@example.com");

        var response = await adminClient.PostAsync(
            $"/api/admin/users/{target.UserId}/achievements/recheck",
            content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<List<AchievementDto>>();
        list.Should().NotBeNull();
    }

    [Fact]
    public async Task Set_telegram_chatid_stores_value()
    {
        var (adminClient, _) = await CreateAuthenticatedClient(asAdmin: true);
        var (_, target) = await CreateUser($"tg-set-{Guid.NewGuid():N}@example.com");
        var chatId = 100_000_000L + Random.Shared.NextInt64(0, 1_000_000_000);

        var response = await adminClient.PutAsJsonAsync(
            $"/api/admin/users/{target.UserId}/telegram",
            new { chatId });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var detailResponse = await adminClient.GetAsync($"/api/admin/users/{target.UserId}");
        var detail = await detailResponse.Content.ReadFromJsonAsync<AdminUserDetailDto>();
        detail!.TelegramChatId.Should().Be(chatId);
    }

    [Fact]
    public async Task Set_telegram_chatid_conflict_when_already_linked()
    {
        var (adminClient, _) = await CreateAuthenticatedClient(asAdmin: true);
        var (_, first) = await CreateUser($"tg-first-{Guid.NewGuid():N}@example.com");
        var (_, second) = await CreateUser($"tg-second-{Guid.NewGuid():N}@example.com");
        var chatId = 200_000_000L + Random.Shared.NextInt64(0, 1_000_000_000);

        var firstLink = await adminClient.PutAsJsonAsync(
            $"/api/admin/users/{first.UserId}/telegram",
            new { chatId });
        firstLink.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondLink = await adminClient.PutAsJsonAsync(
            $"/api/admin/users/{second.UserId}/telegram",
            new { chatId });

        secondLink.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Unlink_telegram_clears_chatid()
    {
        var (adminClient, _) = await CreateAuthenticatedClient(asAdmin: true);
        var (_, target) = await CreateUser($"tg-unlink-{Guid.NewGuid():N}@example.com");
        var chatId = 300_000_000L + Random.Shared.NextInt64(0, 1_000_000_000);

        var link = await adminClient.PutAsJsonAsync(
            $"/api/admin/users/{target.UserId}/telegram",
            new { chatId });
        link.StatusCode.Should().Be(HttpStatusCode.OK);

        var unlink = await adminClient.DeleteAsync($"/api/admin/users/{target.UserId}/telegram");
        unlink.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detailResponse = await adminClient.GetAsync($"/api/admin/users/{target.UserId}");
        var detail = await detailResponse.Content.ReadFromJsonAsync<AdminUserDetailDto>();
        detail!.TelegramChatId.Should().BeNull();
    }

    [Fact]
    public async Task Telegram_endpoints_require_admin_role()
    {
        var (client, _) = await CreateAuthenticatedClient(asAdmin: false);
        var (_, target) = await CreateUser($"tg-auth-{Guid.NewGuid():N}@example.com");

        var putResponse = await client.PutAsJsonAsync(
            $"/api/admin/users/{target.UserId}/telegram",
            new { chatId = 12345L });
        putResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var deleteResponse = await client.DeleteAsync($"/api/admin/users/{target.UserId}/telegram");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<(HttpClient Client, AuthResponse Auth)> CreateUser(string email)
    {
        var client = _fixture.CreateClient();
        var register = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Email = email, Password = "password123" });
        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return (client, auth);
    }

    private async Task<(HttpClient Client, AuthResponse Auth)> CreateAuthenticatedClient(bool asAdmin)
    {
        var client = _fixture.CreateClient();
        var email = $"admin-ach-tg-test-{Guid.NewGuid():N}@example.com";

        var register = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Email = email, Password = "password123" });
        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>();

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
}
