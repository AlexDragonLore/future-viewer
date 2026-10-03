using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.Infrastructure.Persistence;
using FutureViewer.Integration.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class PaymentStatusEndpointTests(IntegrationTestFixture fixture) : IClassFixture<IntegrationTestFixture>
{
    [Fact]
    public async Task Status_requires_authentication()
    {
        using var client = fixture.CreateClient();
        (await client.GetAsync($"/api/payments/{Guid.NewGuid():N}/status")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Only_the_owner_can_check_an_order_and_only_paid_orders_confirm_payment()
    {
        using var client = fixture.CreateClient();
        var owner = await fixture.RegisterAndLoginAsync(client, $"payment-owner-{Guid.NewGuid():N}@example.com", "password123");
        var other = await fixture.RegisterAndLoginAsync(client, $"payment-other-{Guid.NewGuid():N}@example.com", "password123");
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = new PaymentOrder
        {
            UserId = owner.UserId, SubjectReference = Guid.NewGuid(),
            TariffCode = "pro-30d", Amount = 300, Currency = "RUB", AccessDays = 30,
            Provider = "YooKassa", IdempotencyKey = Guid.NewGuid().ToString("N"),
            Status = PaymentOrderStatus.ProviderCreated
        };
        db.PaymentOrders.Add(order);
        await db.SaveChangesAsync();
        var path = $"/api/payments/{order.PublicId:N}/status";

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", other.AccessToken);
        (await client.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", owner.AccessToken);
        var pending = await client.GetFromJsonAsync<PaymentStatusDto>(path);
        pending!.Paid.Should().BeFalse();
        pending.Status.Should().Be("providercreated");

        order.Status = PaymentOrderStatus.Paid;
        order.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        var paid = await client.GetFromJsonAsync<PaymentStatusDto>(path);
        paid!.Paid.Should().BeTrue();
        paid.Status.Should().Be("paid");
        (await client.GetAsync($"/api/payments/{Guid.NewGuid():N}/status")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
