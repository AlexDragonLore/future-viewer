using FutureViewer.Domain.Entities;
using FutureViewer.Domain.Enums;
using FutureViewer.DomainServices.DTOs.Admin;
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

    public async Task<AdminPurchaseListResult> SearchSubscriptionPurchasesAsync(
        Guid? userId, string? search, AdminPurchaseFilter status, int skip, int take,
        CancellationToken ct = default)
    {
        var query = _db.PaymentOrders.AsNoTracking().Where(o => o.AccessDays > 0);
        if (userId.HasValue) query = query.Where(o => o.UserId == userId.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            // Treat wildcard characters as literal search text.
            var pattern = "%" + search.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            var hasPublicId = Guid.TryParse(search, out var publicId);
            query = query.Where(o => (o.User != null && EF.Functions.ILike(o.User.Email, pattern))
                || (hasPublicId && o.PublicId == publicId)
                || (o.ProviderPaymentId != null && EF.Functions.ILike(o.ProviderPaymentId, pattern)));
        }
        if (status == AdminPurchaseFilter.Refunded)
            query = query.Where(o => o.ReceiptStatus == NpdReceiptStatus.Refunded);
        else if (status != AdminPurchaseFilter.All)
        {
            query = query.Where(o => o.ReceiptStatus != NpdReceiptStatus.Refunded);
            query = status switch
            {
                AdminPurchaseFilter.Paid => query.Where(o => o.Status == PaymentOrderStatus.Paid),
                AdminPurchaseFilter.Pending => query.Where(o => o.Status == PaymentOrderStatus.Pending || o.Status == PaymentOrderStatus.ProviderCreated),
                AdminPurchaseFilter.Canceled => query.Where(o => o.Status == PaymentOrderStatus.Canceled),
                AdminPurchaseFilter.Failed => query.Where(o => o.Status == PaymentOrderStatus.Failed),
                _ => throw new ArgumentOutOfRangeException(nameof(status))
            };
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(o => o.CompletedAt ?? o.CreatedAt)
            .ThenByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id)
            .Skip(skip).Take(take)
            .Select(o => new AdminPurchaseDto
            {
                PublicId = o.PublicId, UserId = o.UserId, UserEmail = o.User != null ? o.User.Email : null,
                TariffCode = o.TariffCode, AccessDays = o.AccessDays, Amount = o.Amount, Currency = o.Currency,
                Status = o.ReceiptStatus == NpdReceiptStatus.Refunded ? "refunded"
                    : o.Status == PaymentOrderStatus.Paid ? "paid"
                    : o.Status == PaymentOrderStatus.Canceled ? "canceled"
                    : o.Status == PaymentOrderStatus.Failed ? "failed" : "pending",
                Provider = o.Provider, ProviderPaymentId = o.ProviderPaymentId,
                CreatedAt = o.CreatedAt, CompletedAt = o.CompletedAt
            }).ToListAsync(ct);
        return new AdminPurchaseListResult(items, total);
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

    public async Task<IReadOnlyDictionary<Guid, AdminSubscriptionPurchaseDto>> GetLatestPaidSubscriptionsAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
    {
        if (userIds.Count == 0) return new Dictionary<Guid, AdminSubscriptionPurchaseDto>();
        var purchases = await _db.PaymentOrders.AsNoTracking()
            .Where(o => o.UserId.HasValue && userIds.Contains(o.UserId.Value)
                && o.Status == PaymentOrderStatus.Paid && o.ReceiptStatus != NpdReceiptStatus.Refunded
                && o.AccessDays > 0 && o.CompletedAt != null)
            .GroupBy(o => o.UserId!.Value)
            .Select(group => group.OrderByDescending(o => o.CompletedAt).ThenByDescending(o => o.CreatedAt)
                .ThenByDescending(o => o.Id).Select(o => new
                {
                    UserId = o.UserId!.Value,
                    Purchase = new AdminSubscriptionPurchaseDto
                    {
                        TariffCode = o.TariffCode, AccessDays = o.AccessDays, Amount = o.Amount,
                        Currency = o.Currency, CompletedAt = o.CompletedAt!.Value
                    }
                }).First()).ToListAsync(ct);
        return purchases.ToDictionary(p => p.UserId, p => p.Purchase);
    }
}
