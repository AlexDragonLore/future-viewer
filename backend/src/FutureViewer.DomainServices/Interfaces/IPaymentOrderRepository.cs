using FutureViewer.Domain.Entities;
using FutureViewer.DomainServices.DTOs.Admin;

namespace FutureViewer.DomainServices.Interfaces;

public interface IPaymentOrderRepository
{
    Task<AdminPurchaseListResult> SearchSubscriptionPurchasesAsync(Guid? userId, string? search,
        AdminPurchaseFilter status, int skip, int take, CancellationToken ct = default);
    Task<PaymentOrder> AddAsync(PaymentOrder order, CancellationToken ct = default);
    Task<PaymentOrder?> GetByPublicIdAsync(Guid publicId, CancellationToken ct = default);
    Task<PaymentOrder?> GetByPublicIdForUpdateAsync(Guid publicId, CancellationToken ct = default);
    Task LockOwnerAsync(Guid userId, CancellationToken ct = default);
    Task<PaymentOrder?> GetByProviderPaymentIdAsync(string providerPaymentId, CancellationToken ct = default);
    Task UpdateAsync(PaymentOrder order, CancellationToken ct = default);
    Task<decimal> GetPaidTotalAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, AdminSubscriptionPurchaseDto>> GetLatestPaidSubscriptionsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default);
}
