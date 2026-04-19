using FutureViewer.Domain.Entities;
using FutureViewer.DomainServices.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FutureViewer.Infrastructure.Persistence.Repositories;

public sealed class ProcessedPaymentRepository : IProcessedPaymentRepository
{
    private readonly AppDbContext _db;

    public ProcessedPaymentRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<bool> TryRecordAsync(string paymentId, Guid userId, CancellationToken ct = default)
    {
        var entry = _db.ProcessedPayments.Add(new ProcessedPayment
        {
            PaymentId = paymentId,
            UserId = userId
        });

        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            entry.State = EntityState.Detached;
            return false;
        }
    }
}
