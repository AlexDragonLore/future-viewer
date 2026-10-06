using FluentAssertions;
using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.Interfaces;
using FutureViewer.DomainServices.Services;
using FutureViewer.Infrastructure.Persistence;
using FutureViewer.Infrastructure.Persistence.Repositories;
using FutureViewer.Integration.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FutureViewer.Integration.Tests.Tests;

public sealed class PaymentConcurrencyTests(IntegrationTestFixture fixture) : IClassFixture<IntegrationTestFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Concurrent_payments_preserve_purchased_days_and_replays_grant_only_once(bool replaySameOrder)
    {
        var expiry = DateTime.UtcNow.AddDays(5);
        var user = new User
        {
            Email = $"concurrent-payment-{Guid.NewGuid():N}@example.com",
            PasswordHash = "unused",
            SubscriptionStatus = SubscriptionStatus.Active,
            SubscriptionExpiresAt = expiry
        };
        var first = CreateOrder(user, $"payment-{Guid.NewGuid():N}");
        var second = replaySameOrder ? first : CreateOrder(user, $"payment-{Guid.NewGuid():N}");
        using (var setup = fixture.Services.CreateScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Add(user);
            db.PaymentOrders.Add(first);
            if (!replaySameOrder) db.PaymentOrders.Add(second);
            await db.SaveChangesAsync();
        }

        using var firstScope = fixture.Services.CreateScope();
        using var secondScope = fixture.Services.CreateScope();
        var barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var arrived = 0;
        async Task<PaymentWebhookHandling> Handle(IServiceScope scope, PaymentOrder order)
        {
            if (Interlocked.Increment(ref arrived) == 2) barrier.SetResult();
            await barrier.Task;
            var services = scope.ServiceProvider;
            var service = new SubscriptionService(
                services.GetRequiredService<IUserRepository>(),
                services.GetRequiredService<IReadingRepository>(),
                new VerifiedProvider(order),
                services.GetRequiredService<IProcessedPaymentRepository>(),
                services.GetRequiredService<IUnitOfWork>(),
                services.GetRequiredService<IPaymentOrderRepository>());
            return await service.ProcessWebhookWithOutcomeAsync("verified-test-notification");
        }

        var results = await Task.WhenAll(Handle(firstScope, first), Handle(secondScope, second));

        using var verification = fixture.Services.CreateScope();
        var database = verification.ServiceProvider.GetRequiredService<AppDbContext>();
        var persistedUser = await database.Users.AsNoTracking().SingleAsync(x => x.Id == user.Id);
        persistedUser.SubscriptionExpiresAt.Should().BeCloseTo(
            expiry.AddDays(replaySameOrder ? 30 : 60), TimeSpan.FromMilliseconds(1));
        results.Count(x => x == PaymentWebhookHandling.Processed).Should().Be(replaySameOrder ? 1 : 2);
        (await database.ProcessedPayments.CountAsync(x => x.SubjectReference == user.PrivacySubjectId))
            .Should().Be(replaySameOrder ? 1 : 2);
    }

    [Fact]
    public async Task Non_duplicate_payment_storage_errors_propagate_for_provider_retry()
    {
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new User { Email = $"payment-error-{Guid.NewGuid():N}@example.com", PasswordHash = "unused" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var repository = new ProcessedPaymentRepository(db);

        await repository.Invoking(x => x.TryRecordAsync(new string('x', 129), user.Id))
            .Should().ThrowAsync<Npgsql.PostgresException>();
    }

    private static PaymentOrder CreateOrder(User user, string paymentId) => new()
    {
        UserId = user.Id,
        SubjectReference = user.PrivacySubjectId,
        TariffCode = "pro-30d",
        Amount = 300m,
        Currency = "RUB",
        AccessDays = 30,
        Provider = "verified-test",
        IdempotencyKey = Guid.NewGuid().ToString("N"),
        ProviderPaymentId = paymentId,
        Status = PaymentOrderStatus.ProviderCreated
    };

    private sealed class VerifiedProvider(PaymentOrder order) : IPaymentProvider
    {
        public string ProviderName => "verified-test";
        public PaymentProductDescriptor Product => new()
        {
            TariffCode = "pro-30d", Amount = 300m, Currency = "RUB", AccessDays = 30
        };

        public Task<PaymentCreationResult> CreateSubscriptionPaymentAsync(
            Guid publicOrderId, string idempotencyKey, PaymentProductDescriptor product, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public PaymentWebhookEvent ParseWebhook(string body) => new()
        {
            Type = PaymentWebhookEventType.PaymentSucceeded,
            PaymentId = order.ProviderPaymentId!
        };

        public Task<PaymentVerification?> VerifyPaymentAsync(string paymentId, CancellationToken ct = default) =>
            Task.FromResult<PaymentVerification?>(new PaymentVerification
            {
                PaymentId = paymentId,
                Paid = true,
                Status = "succeeded",
                OrderId = order.PublicId,
                Amount = order.Amount,
                Currency = order.Currency
            });
    }
}
