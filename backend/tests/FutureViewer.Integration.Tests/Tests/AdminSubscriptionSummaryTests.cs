using System.Data.Common;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.DTOs.Admin;
using FutureViewer.DomainServices.Services;
using FutureViewer.Infrastructure.Persistence;
using FutureViewer.Infrastructure.Persistence.Repositories;
using FutureViewer.Integration.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class AdminSubscriptionSummaryTests(IntegrationTestFixture fixture) : IClassFixture<IntegrationTestFixture>
{
    [Fact]
    public async Task Purchases_are_actual_paid_orders_separate_from_manual_or_expired_access_and_available_without_readings()
    {
        var marker = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;
        var buyer = User(marker, "buyer", now.AddDays(60));
        buyer.YukassaSubscriptionId = "unrelated-current-provider-reference";
        var manual = User(marker, "manual", now.AddDays(10));
        var expired = User(marker, "expired", now.AddDays(-1));
        var pending = User(marker, "pending", null);
        var refunded = User(marker, "refunded", null);
        var latest = Order(buyer, "pro-30d", 30, 299m, now.AddDays(-2));
        var refundedOrder = Order(refunded, "pro-30d", 30, 299m, now.AddDays(-1));
        refundedOrder.ReceiptStatus = NpdReceiptStatus.Refunded;
        var pendingOrder = Order(pending, "pro-30d", 30, 299m, now);
        pendingOrder.Status = PaymentOrderStatus.ProviderCreated;
        var newerPending = Order(buyer, "pending-is-not-purchased", 30, 299m, now);
        newerPending.Status = PaymentOrderStatus.Pending;
        var newerRefund = Order(buyer, "refunded-is-not-current-purchase", 30, 299m, now);
        newerRefund.ReceiptStatus = NpdReceiptStatus.Refunded;
        var newerCanceled = Order(buyer, "canceled", 30, 299m, now);
        newerCanceled.Status = PaymentOrderStatus.Canceled;
        var noCompletion = Order(buyer, "missing-completion", 30, 299m, now);
        noCompletion.CompletedAt = null;
        var notSubscription = Order(buyer, "zero-access-product", 0, 99m, now);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.AddRange(buyer, manual, expired, pending, refunded);
            db.PaymentOrders.AddRange(latest, Order(buyer, "pro-7d", 7, 99m, now.AddDays(-10)),
                Order(expired, "pro-7d", 7, 99m, now.AddDays(-20)), refundedOrder, pendingOrder,
                newerPending, newerRefund, newerCanceled, noCompletion, notSubscription);
            db.Readings.AddRange(Reading(buyer, marker), Reading(manual, marker), Reading(expired, marker));
            await db.SaveChangesAsync();
        }
        var admin = await AdminClient();
        var users = (await admin.GetFromJsonAsync<AdminUserListResult>($"/api/admin/users?search={marker}"))!;
        users.Total.Should().Be(5);
        var subscriptions = users.Items.ToDictionary(u => u.Id, u => u.Subscription!);
        subscriptions[buyer.Id].IsActive.Should().BeTrue();
        var purchase = subscriptions[buyer.Id].LastPurchase!;
        purchase.TariffCode.Should().Be("pro-30d");
        purchase.AccessDays.Should().Be(30);
        purchase.Amount.Should().Be(299m);
        purchase.Currency.Should().Be("RUB");
        purchase.CompletedAt.Should().BeCloseTo(latest.CompletedAt!.Value, TimeSpan.FromMicroseconds(1));
        subscriptions[manual.Id].IsActive.Should().BeTrue();
        subscriptions[manual.Id].LastPurchase.Should().BeNull();
        subscriptions[expired.Id].Status.Should().Be(SubscriptionStatus.Active);
        subscriptions[expired.Id].IsActive.Should().BeFalse();
        subscriptions[expired.Id].LastPurchase!.TariffCode.Should().Be("pro-7d");
        subscriptions[pending.Id].LastPurchase.Should().BeNull();
        subscriptions[pending.Id].IsActive.Should().BeFalse();
        subscriptions[refunded.Id].LastPurchase.Should().BeNull();

        var readings = (await admin.GetFromJsonAsync<AdminReadingListResult>($"/api/admin/readings?search={marker}&includeInterpretation=false"))!;
        foreach (var reading in readings.Items)
            reading.Subscription.Should().BeEquivalentTo(subscriptions[reading.UserId!.Value]);
        foreach (var user in users.Items)
        {
            var detail = (await admin.GetFromJsonAsync<AdminUserDetailDto>($"/api/admin/users/{user.Id}"))!;
            detail.Subscription.Should().BeEquivalentTo(subscriptions[user.Id]);
        }
    }

    [Fact]
    public async Task Pages_use_bounded_queries_and_do_not_select_private_user_fields_or_answer_bodies()
    {
        var marker = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;
        using var scope = fixture.Services.CreateScope();
        var original = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = Enumerable.Range(1, 20).Select(i => User(marker, $"{i:D2}", now.AddDays(7))).ToArray();
        original.Users.AddRange(users);
        foreach (var user in users)
        {
            var reading = Reading(user, marker);
            original.Readings.Add(reading);
            original.PaymentOrders.Add(Order(user, "pro-7d", 7, 99m, now));
        }
        await original.SaveChangesAsync();
        var capture = new QueryCapture();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(original.Database.GetConnectionString()).AddInterceptors(capture).Options);
        var readings = new ReadingRepository(db);
        var userRepository = new UserRepository(db);
        var achievements = new AchievementRepository(db);
        var service = new AdminService(readings, userRepository, achievements,
            new AchievementService(achievements, readings, userRepository),
            NullLogger<AdminService>.Instance, orders: new PaymentOrderRepository(db));

        var userPage = await service.SearchUsersAsync(marker, 1, 20);
        userPage.Items.Should().HaveCount(20).And.OnlyContain(u => u.TotalReadings == 1);
        userPage.Items.Should().OnlyContain(u => u.Subscription!.LastPurchase!.TariffCode == "pro-7d");
        capture.Commands.Should().HaveCount(3);
        capture.Commands.Should().OnlyContain(sql => !sql.Contains("password_hash") && !sql.Contains("self_report"));
        capture.Commands.Count(sql => sql.Contains("payment_orders")).Should().Be(1);
        capture.Commands.Clear();

        foreach (var audience in new[] { AdminReadingAudience.All, AdminReadingAudience.Subscribers, AdminReadingAudience.Registered })
        {
            capture.Commands.Clear();
            var readingPage = await service.SearchReadingsAsync(null, marker, 1, 20, includeInterpretation: false, audience: audience);
            readingPage.Items.Should().HaveCount(20).And.OnlyContain(r => r.HasInterpretation && r.Interpretation == null);
            capture.Commands.Should().HaveCount(3);
            capture.Commands.Should().OnlyContain(sql => !sql.Contains("password_hash"));
            capture.Commands.Count(sql => sql.Contains("payment_orders")).Should().Be(1);
            capture.Fields.Should().NotContain("Interpretation");
        }
        capture.Commands.Clear();
        (await service.SearchReadingsAsync(null, marker, 1, 20, audience: AdminReadingAudience.Guests)).Items.Should().BeEmpty();
        capture.Commands.Should().HaveCount(2).And.OnlyContain(sql => !sql.Contains("payment_orders"));
        db.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task User_pagination_is_stable_for_equal_dates_and_clamps_overflowing_page_numbers()
    {
        var marker = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;
        var users = Enumerable.Range(1, 3).Select(i => new User
        {
            Id = Guid.Parse($"11111111-0000-0000-0000-{i:D12}"), Email = $"{marker}-{i}@example.com",
            PasswordHash = "unused", CreatedAt = now
        }).ToArray();
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.AddRange(users);
            await db.SaveChangesAsync();
        }
        var admin = await AdminClient();
        for (var page = 1; page <= 3; page++)
        {
            var result = (await admin.GetFromJsonAsync<AdminUserListResult>($"/api/admin/users?search={marker}&page={page}&pageSize=1"))!;
            result.Items.Should().ContainSingle().Which.Id.Should().Be(users[3 - page].Id);
        }
        var beyond = (await admin.GetFromJsonAsync<AdminUserListResult>($"/api/admin/users?search={marker}&page={int.MaxValue}&pageSize=100"))!;
        beyond.Total.Should().Be(3);
        beyond.Items.Should().BeEmpty();
    }

    private static User User(string marker, string suffix, DateTime? expiresAt) => new()
    {
        Email = $"{marker}-{suffix}@example.com", PasswordHash = "unused",
        SubscriptionStatus = SubscriptionStatus.Active, SubscriptionExpiresAt = expiresAt
    };

    private static Reading Reading(User user, string marker) => new()
    {
        UserId = user.Id, SpreadType = SpreadType.SingleCard, Question = $"{marker} Вопрос",
        AiInterpretation = new string('a', 50_000), SavedToHistory = true
    };

    private static PaymentOrder Order(User user, string tariff, int days, decimal amount, DateTime completedAt) => new()
    {
        UserId = user.Id, SubjectReference = user.PrivacySubjectId, TariffCode = tariff, AccessDays = days,
        Amount = amount, Currency = "RUB", Provider = "test", IdempotencyKey = Guid.NewGuid().ToString("N"),
        Status = PaymentOrderStatus.Paid, CompletedAt = completedAt
    };

    private async Task<HttpClient> AdminClient()
    {
        var client = fixture.CreateClient();
        var email = $"admin-subscriptions-{Guid.NewGuid():N}@example.com";
        var auth = await fixture.RegisterAndLoginAsync(client, email, "password123");
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Users.FindAsync(auth.UserId))!.IsAdmin = true;
        await db.SaveChangesAsync();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = "password123" });
        login.EnsureSuccessStatusCode();
        auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private sealed class QueryCapture : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];
        public List<string> Fields { get; } = [];
        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            Fields.AddRange(Enumerable.Range(0, result.FieldCount).Select(result.GetName));
            return new ValueTask<DbDataReader>(result);
        }
    }
}
