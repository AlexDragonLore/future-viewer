using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.DTOs.Admin;
using FutureViewer.Infrastructure.Persistence;
using FutureViewer.Infrastructure.Persistence.Repositories;
using FutureViewer.Integration.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class AdminReadingAudienceTests(IntegrationTestFixture fixture) : IClassFixture<IntegrationTestFixture>
{
    [Fact]
    public async Task Audiences_intersect_search_and_owner_before_counting_and_paging_and_preserve_retention()
    {
        var now = DateTime.UtcNow;
        var marker = Guid.NewGuid().ToString("N");
        var active = User(SubscriptionStatus.Active, now.AddDays(7));
        var manual = User(SubscriptionStatus.Active, now.AddDays(30));
        var free = User(SubscriptionStatus.None, null);
        var expired = User(SubscriptionStatus.Active, now.AddSeconds(-1));
        var noExpiry = User(SubscriptionStatus.Active, null);
        var cancelled = User(SubscriptionStatus.Cancelled, now.AddDays(7));
        var inactive = User(SubscriptionStatus.None, now.AddDays(7));
        var expiredStatus = User(SubscriptionStatus.Expired, now.AddDays(7));
        var users = new[] { active, manual, free, expired, noExpiry, cancelled, inactive, expiredStatus };
        var registered = users.Select((u, i) => Reading(u.Id, $"{marker} Вопрос {i}", now.AddMinutes(-i))).ToArray();
        registered[2].DeletedFromHistoryAt = now; // Admins retain hidden registered history.
        var secondActive = Reading(active.Id, $"{marker} Ещё один вопрос", now.AddDays(-1));
        var guest = Reading(null, $"{marker} Гость", now.AddDays(-29));
        var expiredGuest = Reading(null, $"{marker} Истёкший гость", now.AddDays(-30));
        var unsaved = Reading(active.Id, $"{marker} Несохранённый", now);
        unsaved.SavedToHistory = false;
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.AddRange(users);
            db.Readings.AddRange(registered);
            db.Readings.AddRange(secondActive, guest, expiredGuest, unsaved,
                Reading(null, string.Empty, now), Reading(active.Id, "Вне поиска", now));
            await db.SaveChangesAsync();
        }
        var admin = await Client(admin: true);
        var allIds = registered.Select(r => r.Id).Concat([secondActive.Id, guest.Id]).ToArray();
        foreach (var audience in new[] { "", "&audience=", "&audience=all" })
        {
            var all = await Read(admin, $"search={marker}{audience}");
            all.Total.Should().Be(10);
            all.Items.Select(r => r.Id).Should().BeEquivalentTo(allIds);
            all.Items.Should().OnlyContain(r => r.Interpretation == "Полный ответ");
        }
        var subscribed = await Read(admin, $"search={marker}&audience=subscribers&pageSize=2&includeInterpretation=false");
        subscribed.Total.Should().Be(3);
        subscribed.Items.Select(r => r.Id).Should().Equal(registered[0].Id, registered[1].Id);
        subscribed.Items.Should().OnlyContain(r => r.Subscription!.IsActive && r.Interpretation == null && r.HasInterpretation);
        subscribed.Items.Single(r => r.UserId == manual.Id).Subscription!.LastPurchase.Should().BeNull();
        var secondPage = await Read(admin, $"search={marker}&audience=subscribers&page=2&pageSize=2");
        secondPage.Total.Should().Be(3);
        secondPage.Items.Should().ContainSingle().Which.Id.Should().Be(secondActive.Id);
        (await Read(admin, $"search={marker}&audience=subscribers&page=3&pageSize=2")).Items.Should().BeEmpty();

        var owned = await Read(admin, $"search={marker}&audience=registered");
        owned.Total.Should().Be(9);
        owned.Items.Select(r => r.Id).Should().BeEquivalentTo(registered.Select(r => r.Id).Append(secondActive.Id));
        owned.Items.Should().OnlyContain(r => r.UserId != null);
        var anonymous = await Read(admin, $"search={marker}&audience=guests&includeInterpretation=false");
        anonymous.Total.Should().Be(1);
        anonymous.Items.Should().ContainSingle().Which.Id.Should().Be(guest.Id);

        (await Read(admin, $"search={marker}&audience=subscribers&userId={active.Id}")).Total.Should().Be(2);
        (await Read(admin, $"search={marker}&audience=subscribers&userId={free.Id}")).Total.Should().Be(0);
        (await Read(admin, $"search={marker}&audience=guests&userId={active.Id}")).Total.Should().Be(0);
        (await Read(admin, $"search=absent-{marker}&audience=registered")).Total.Should().Be(0);
    }

    [Fact]
    public async Task Subscriber_expiry_boundary_uses_the_same_instant_for_list_total_and_badge()
    {
        var instant = new DateTime(2026, 10, 17, 0, 0, 0, DateTimeKind.Utc);
        var marker = Guid.NewGuid().ToString("N");
        var future = User(SubscriptionStatus.Active, instant.AddMicroseconds(1));
        var boundary = User(SubscriptionStatus.Active, instant);
        var past = User(SubscriptionStatus.Active, instant.AddMicroseconds(-1));
        var missing = User(SubscriptionStatus.Active, null);
        var inactive = User(SubscriptionStatus.Expired, instant.AddDays(1));
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = new[] { future, boundary, past, missing, inactive };
        db.Users.AddRange(users);
        db.Readings.AddRange(users.Select(u => Reading(u.Id, marker, instant)));
        await db.SaveChangesAsync();
        var repository = new ReadingRepository(db);
        var rows = await repository.SearchAdminListAsync(null, marker, 0, 20, false,
            audience: AdminReadingAudience.Subscribers, asOfUtc: instant);
        var total = await repository.CountForAdminAsync(null, marker,
            audience: AdminReadingAudience.Subscribers, asOfUtc: instant);
        total.Should().Be(1);
        rows.Should().ContainSingle().Which.UserId.Should().Be(future.Id);
        rows.Single().Subscription!.IsActive.Should().BeTrue();
    }

    [Theory]
    [InlineData("paid")]
    [InlineData("1")]
    [InlineData("Subscribers")]
    [InlineData("all,guests")]
    public async Task Unknown_audience_is_rejected_for_admins(string audience)
    {
        var admin = await Client(admin: true);
        var response = await admin.GetAsync($"/api/admin/readings?audience={Uri.EscapeDataString(audience)}");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("invalid_audience");
    }

    [Fact]
    public async Task Audience_filters_do_not_bypass_admin_authorization()
    {
        var ordinary = await Client();
        foreach (var audience in new[] { "all", "subscribers", "registered", "guests" })
        {
            (await fixture.CreateClient().GetAsync($"/api/admin/readings?audience={audience}"))
                .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await ordinary.GetAsync($"/api/admin/readings?audience={audience}"))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }

    private static User User(SubscriptionStatus status, DateTime? expires) => new()
    {
        Email = $"audience-{Guid.NewGuid():N}@example.com", PasswordHash = "unused",
        SubscriptionStatus = status, SubscriptionExpiresAt = expires
    };

    private static Reading Reading(Guid? owner, string question, DateTime at) => new()
    {
        UserId = owner, Question = question, SpreadType = SpreadType.SingleCard,
        AiInterpretation = "Полный ответ", CreatedAt = at, SavedToHistory = owner != null
    };

    private static async Task<AdminReadingListResult> Read(HttpClient client, string query)
    {
        var response = await client.GetAsync($"/api/admin/readings?{query}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AdminReadingListResult>())!;
    }

    private async Task<HttpClient> Client(bool admin = false)
    {
        var client = fixture.CreateClient();
        var email = $"audience-auth-{Guid.NewGuid():N}@example.com";
        var auth = await fixture.RegisterAndLoginAsync(client, email, "password123");
        if (admin)
        {
            using var scope = fixture.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Users.FindAsync(auth.UserId))!.IsAdmin = true;
            await db.SaveChangesAsync();
            var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = "password123" });
            response.EnsureSuccessStatusCode();
            auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        }
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}
