using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.DTOs.Admin;
using FutureViewer.DomainServices.Interfaces;
using FutureViewer.Integration.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class AdminUserEndpointTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public AdminUserEndpointTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Admin_users_list_requires_admin_role()
    {
        var (client, _) = await CreateAuthenticatedClient(asAdmin: false);

        var response = await client.GetAsync("/api/admin/users");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_users_list_returns_paged_results_and_finds_user_by_search()
    {
        var (adminClient, _) = await CreateAuthenticatedClient(asAdmin: true);
        var targetEmail = $"search-me-{Guid.NewGuid():N}@example.com";
        var (_, target) = await CreateUser(targetEmail);

        var response = await adminClient.GetAsync($"/api/admin/users?search={Uri.EscapeDataString("search-me")}&page=1&pageSize=50");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<AdminUserList>();
        payload.Should().NotBeNull();
        payload!.Items.Should().Contain(u => u.Id == target.UserId && u.Email == targetEmail);
        payload.Total.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Admin_users_detail_includes_stats_and_recent_readings()
    {
        var (adminClient, _) = await CreateAuthenticatedClient(asAdmin: true);
        var (userClient, user) = await CreateAuthenticatedSubscribedClient();
        await CreateReading(userClient);

        var response = await adminClient.GetAsync($"/api/admin/users/{user.UserId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await response.Content.ReadFromJsonAsync<AdminUserDetailDto>();
        detail.Should().NotBeNull();
        detail!.Id.Should().Be(user.UserId);
        detail.Email.Should().Be(user.Email);
        detail.TotalReadings.Should().BeGreaterThanOrEqualTo(1);
        detail.RecentReadings.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Admin_can_toggle_admin_flag_on_another_user()
    {
        var (adminClient, _) = await CreateAuthenticatedClient(asAdmin: true);
        var (_, target) = await CreateUser($"promote-{Guid.NewGuid():N}@example.com");

        var grant = await adminClient.PutAsJsonAsync($"/api/admin/users/{target.UserId}/admin", new { isAdmin = true });
        grant.StatusCode.Should().Be(HttpStatusCode.OK);
        var item = await grant.Content.ReadFromJsonAsync<AdminUserListItem>();
        item!.IsAdmin.Should().BeTrue();

        var revoke = await adminClient.PutAsJsonAsync($"/api/admin/users/{target.UserId}/admin", new { isAdmin = false });
        revoke.StatusCode.Should().Be(HttpStatusCode.OK);
        var item2 = await revoke.Content.ReadFromJsonAsync<AdminUserListItem>();
        item2!.IsAdmin.Should().BeFalse();
    }

    [Fact]
    public async Task Admin_cannot_revoke_own_admin_role()
    {
        var (adminClient, admin) = await CreateAuthenticatedClient(asAdmin: true);

        var response = await adminClient.PutAsJsonAsync($"/api/admin/users/{admin.UserId}/admin", new { isAdmin = false });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Admin_can_set_subscription_active_with_default_expiry()
    {
        var (adminClient, _) = await CreateAuthenticatedClient(asAdmin: true);
        var (_, target) = await CreateUser($"sub-{Guid.NewGuid():N}@example.com");

        var response = await adminClient.PutAsJsonAsync(
            $"/api/admin/users/{target.UserId}/subscription",
            new { status = SubscriptionStatus.Active });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await response.Content.ReadFromJsonAsync<AdminUserDetailDto>();
        detail!.SubscriptionStatus.Should().Be(SubscriptionStatus.Active);
        detail.SubscriptionExpiresAt.Should().NotBeNull();
        detail.SubscriptionExpiresAt!.Value.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task Admin_can_set_subscription_with_explicit_expiry()
    {
        var (adminClient, _) = await CreateAuthenticatedClient(asAdmin: true);
        var (_, target) = await CreateUser($"sub-exp-{Guid.NewGuid():N}@example.com");
        var expiry = DateTime.UtcNow.AddDays(90);

        var response = await adminClient.PutAsJsonAsync(
            $"/api/admin/users/{target.UserId}/subscription",
            new { status = SubscriptionStatus.Active, expiresAt = expiry });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await response.Content.ReadFromJsonAsync<AdminUserDetailDto>();
        detail!.SubscriptionExpiresAt.Should().NotBeNull();
        detail.SubscriptionExpiresAt!.Value.Should().BeCloseTo(expiry, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Admin_cannot_delete_themselves()
    {
        var (adminClient, admin) = await CreateAuthenticatedClient(asAdmin: true);

        var response = await adminClient.DeleteAsync($"/api/admin/users/{admin.UserId}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Admin_can_delete_user_and_cascades_related_data()
    {
        var (adminClient, _) = await CreateAuthenticatedClient(asAdmin: true);
        var (userClient, target) = await CreateAuthenticatedSubscribedClient();
        await CreateReading(userClient);

        var delete = await adminClient.DeleteAsync($"/api/admin/users/{target.UserId}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterGet = await adminClient.GetAsync($"/api/admin/users/{target.UserId}");
        afterGet.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<Guid> CreateReading(HttpClient client)
    {
        var created = await client.PostAsJsonAsync("/api/readings",
            new CreateReadingRequest { SpreadType = SpreadType.SingleCard, Question = "Focus?" });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var reading = await created.Content.ReadFromJsonAsync<ReadingResult>();
        return reading!.Id;
    }

    private async Task<(HttpClient Client, AuthResponse Auth)> CreateUser(string email)
    {
        var client = _fixture.CreateClient();
        var auth = await _fixture.RegisterAndLoginAsync(client, email, "password123");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return (client, auth);
    }

    private async Task<(HttpClient Client, AuthResponse Auth)> CreateAuthenticatedClient(bool asAdmin)
    {
        var client = _fixture.CreateClient();
        var email = $"admin-user-test-{Guid.NewGuid():N}@example.com";

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

    private async Task<(HttpClient Client, AuthResponse Auth)> CreateAuthenticatedSubscribedClient()
    {
        var client = _fixture.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@example.com";

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

    private sealed class AdminUserList
    {
        public List<AdminUserListItem> Items { get; init; } = new();
        public int Total { get; init; }
    }
}
