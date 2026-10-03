namespace FutureViewer.DomainServices.Interfaces;

public interface IProcessedPaymentRepository
{
    Task<bool> ExistsAsync(string paymentId, CancellationToken ct = default);
    Task<bool> TryRecordAsync(string paymentId, Guid userId, CancellationToken ct = default);
}
