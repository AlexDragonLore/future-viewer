namespace FutureViewer.DomainServices.Interfaces;

public interface IProcessedPaymentRepository
{
    Task<bool> TryRecordAsync(string paymentId, Guid userId, CancellationToken ct = default);
}
