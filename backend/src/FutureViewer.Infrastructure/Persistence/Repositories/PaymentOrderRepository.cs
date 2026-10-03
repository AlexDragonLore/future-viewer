using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FutureViewer.Infrastructure.Persistence.Repositories;

public sealed class PaymentOrderRepository : IPaymentOrderRepository
{
    private readonly AppDbContext _db;

    public PaymentOrderRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PaymentOrder> AddAsync(PaymentOrder order, CancellationToken ct = default)
    {
        await _db.PaymentOrders.AddAsync(order, ct);
        await _db.SaveChangesAsync(ct);
        return order;
    }

    public Task<PaymentOrder?> GetByPublicIdAsync(Guid publicId, CancellationToken ct = default) =>
        _db.PaymentOrders.SingleOrDefaultAsync(x => x.PublicId == publicId, ct);

    public Task<PaymentOrder?> GetByPublicIdForUpdateAsync(Guid publicId, CancellationToken ct = default)
    {
        RequireTransaction();
        return _db.PaymentOrders
            .FromSqlInterpolated($"SELECT * FROM payment_orders WHERE public_id = {publicId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
    }

    public async Task LockOwnerAsync(Guid userId, CancellationToken ct = default)
    {
        RequireTransaction();
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM users WHERE id = {userId} FOR UPDATE", ct);
    }

    private void RequireTransaction()
    {
        if (_db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Payment locks require an active transaction.");
    }

    public Task<PaymentOrder?> GetByProviderPaymentIdAsync(string providerPaymentId, CancellationToken ct = default) =>
        _db.PaymentOrders.SingleOrDefaultAsync(x => x.ProviderPaymentId == providerPaymentId, ct);

    public async Task UpdateAsync(PaymentOrder order, CancellationToken ct = default)
    {
        if (_db.Entry(order).State == EntityState.Detached)
            _db.PaymentOrders.Update(order);
        await _db.SaveChangesAsync(ct);
    }

    public Task<decimal> GetPaidTotalAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) =>
        _db.PaymentOrders
            .Where(x => x.Status == PaymentOrderStatus.Paid &&
                        x.CompletedAt >= fromUtc && x.CompletedAt < toUtc)
            .SumAsync(x => x.Amount, ct);
}
