using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.DTOs.Admin;
using FutureViewer.Infrastructure.Persistence;
using FutureViewer.Integration.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class AdminPurchaseEndpointTests(IntegrationTestFixture fixture) : IClassFixture<IntegrationTestFixture>
{
    [Fact]
    public async Task History_requires_admin_and_rejects_unknown_status()
    {
        using var anonymous = fixture.CreateClient();
        (await anonymous.GetAsync("/api/admin/purchases")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var user = await Client(false);
        (await user.GetAsync("/api/admin/purchases")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var admin = await Client();
        (await admin.GetAsync("/api/admin/purchases?status=2")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.GetAsync("/api/admin/purchases?status=unknown")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task History_lists_repeat_purchases_independent_of_current_access_and_excludes_manual_grants()
    {
        var buyer = Buyer();
        buyer.SubscriptionStatus = SubscriptionStatus.Expired;
        buyer.SubscriptionExpiresAt = DateTime.UtcNow.AddDays(-1);
        var old = Order(buyer, PaymentOrderStatus.Paid, -10);
        var recent = Order(buyer, PaymentOrderStatus.Paid, -2);
        var manual = Buyer();
        manual.SubscriptionStatus = SubscriptionStatus.Active;
        manual.SubscriptionExpiresAt = DateTime.UtcNow.AddDays(7);
        var otherProduct = Order(buyer, PaymentOrderStatus.Paid, 0, accessDays: 0);
        await Seed([buyer, manual], [old, recent, otherProduct]);
        using var client = await Client();
        var response = await client.GetAsync($"/api/admin/purchases?userId={buyer.Id}");
        response.EnsureSuccessStatusCode();
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        var result = (await response.Content.ReadFromJsonAsync<AdminPurchaseListResult>())!;
        result.Total.Should().Be(2);
        result.Items.Select(p => p.PublicId).Should().Equal(recent.PublicId, old.PublicId);
        result.Items.Should().OnlyContain(p => p.UserEmail == buyer.Email && p.Status == "paid" && p.Amount == 299m);
        var manualHistory = (await client.GetFromJsonAsync<AdminPurchaseListResult>($"/api/admin/purchases?userId={manual.Id}&status=all"))!;
        manualHistory.Total.Should().Be(0);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("items")[0].EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
            "publicId", "userId", "userEmail", "tariffCode", "accessDays", "amount", "currency", "status",
            "provider", "providerPaymentId", "createdAt", "completedAt");
    }

    [Fact]
    public async Task Status_search_and_user_filters_combine_and_refunds_are_not_paid()
    {
        var buyer = Buyer();
        var orders = Enum.GetValues<PaymentOrderStatus>().Select(s => Order(buyer, s, 0)).ToList();
        var refund = Order(buyer, PaymentOrderStatus.Paid, 1);
        refund.ReceiptStatus = NpdReceiptStatus.Refunded;
        orders.Add(refund);
        await Seed([buyer], orders);
        using var client = await Client();
        foreach (var (status, count) in new[] { ("paid", 1), ("pending", 2), ("canceled", 1), ("failed", 1), ("refunded", 1), ("all", 6) })
        {
            var result = (await client.GetFromJsonAsync<AdminPurchaseListResult>(
                $"/api/admin/purchases?userId={buyer.Id}&search={Uri.EscapeDataString(buyer.Email.ToUpperInvariant())}&status={status}"))!;
            result.Total.Should().Be(count);
            if (status != "all") result.Items.Should().OnlyContain(p => p.Status == status);
        }
        foreach (var search in new[] { refund.PublicId.ToString(), refund.ProviderPaymentId! })
        {
            var result = (await client.GetFromJsonAsync<AdminPurchaseListResult>(
                $"/api/admin/purchases?search={Uri.EscapeDataString(search)}&status=all"))!;
            result.Items.Should().ContainSingle().Which.PublicId.Should().Be(refund.PublicId);
        }
        var absent = (await client.GetFromJsonAsync<AdminPurchaseListResult>(
            $"/api/admin/purchases?userId={Guid.NewGuid()}&status=all&search={buyer.Email}"))!;
        absent.Total.Should().Be(0);
        var literal = (await client.GetFromJsonAsync<AdminPurchaseListResult>(
            $"/api/admin/purchases?userId={buyer.Id}&search=%25&status=all"))!;
        literal.Total.Should().Be(0);
    }

    [Fact]
    public async Task Deleted_buyers_keep_anonymized_history_and_orders_without_provider_ids_remain_visible()
    {
        var buyer = Buyer();
        var paid = Order(buyer, PaymentOrderStatus.Paid, 0);
        var pending = Order(buyer, PaymentOrderStatus.Pending, 1);
        pending.ProviderPaymentId = null;
        await Seed([buyer], [paid, pending]);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Remove((await db.Users.FindAsync(buyer.Id))!);
            await db.SaveChangesAsync();
        }
        using var client = await Client();
        foreach (var order in new[] { paid, pending })
        {
            var result = (await client.GetFromJsonAsync<AdminPurchaseListResult>(
                $"/api/admin/purchases?search={order.PublicId}&status=all"))!;
            var item = result.Items.Should().ContainSingle().Subject;
            item.UserId.Should().BeNull();
            item.UserEmail.Should().BeNull();
            item.ProviderPaymentId.Should().Be(order.ProviderPaymentId);
        }
    }

    [Fact]
    public async Task Pagination_is_stable_bounded_and_handles_extreme_inputs()
    {
        var buyer = Buyer();
        var timestamp = DateTime.UtcNow;
        var orders = Enumerable.Range(0, 105).Select(_ => Order(buyer, PaymentOrderStatus.Paid, 0, timestamp: timestamp)).ToList();
        await Seed([buyer], orders);
        using var client = await Client();
        var first = (await client.GetFromJsonAsync<AdminPurchaseListResult>(
            $"/api/admin/purchases?userId={buyer.Id}&page=0&pageSize=1000"))!;
        first.Total.Should().Be(105);
        first.Items.Should().HaveCount(100);
        var second = (await client.GetFromJsonAsync<AdminPurchaseListResult>(
            $"/api/admin/purchases?userId={buyer.Id}&page=2&pageSize=100"))!;
        second.Items.Should().HaveCount(5);
        first.Items.Select(p => p.PublicId).Intersect(second.Items.Select(p => p.PublicId)).Should().BeEmpty();
        var again = (await client.GetFromJsonAsync<AdminPurchaseListResult>(
            $"/api/admin/purchases?userId={buyer.Id}&page=1&pageSize=100"))!;
        again.Items.Select(p => p.PublicId).Should().Equal(first.Items.Select(p => p.PublicId));
        var beyond = (await client.GetFromJsonAsync<AdminPurchaseListResult>(
            $"/api/admin/purchases?userId={buyer.Id}&page={int.MaxValue}&pageSize=100"))!;
        beyond.Total.Should().Be(105);
        beyond.Items.Should().BeEmpty();
        var minimum = (await client.GetFromJsonAsync<AdminPurchaseListResult>(
            $"/api/admin/purchases?userId={buyer.Id}&page=-1&pageSize=-1"))!;
        minimum.Items.Should().ContainSingle();
    }

    private static User Buyer() => new() { Email = $"buyer-{Guid.NewGuid():N}@example.com", PasswordHash = "unused" };

    private static PaymentOrder Order(User user, PaymentOrderStatus status, int offset, int accessDays = 30, DateTime? timestamp = null) => new()
    {
        UserId = user.Id, SubjectReference = user.PrivacySubjectId, TariffCode = "pro-30d", AccessDays = accessDays,
        Amount = 299m, Currency = "RUB", Provider = "test", IdempotencyKey = Guid.NewGuid().ToString("N"),
        ProviderPaymentId = $"payment-{Guid.NewGuid():N}", Status = status,
        CreatedAt = (timestamp ?? DateTime.UtcNow).AddDays(offset),
        CompletedAt = status == PaymentOrderStatus.Paid ? (timestamp ?? DateTime.UtcNow).AddDays(offset) : null
    };

    private async Task Seed(IEnumerable<User> users, IEnumerable<PaymentOrder> orders)
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Users.AddRange(users);
        db.PaymentOrders.AddRange(orders);
        await db.SaveChangesAsync();
    }

    private async Task<HttpClient> Client(bool admin = true)
    {
        var client = fixture.CreateClient();
        var email = $"purchase-admin-{Guid.NewGuid():N}@example.com";
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
