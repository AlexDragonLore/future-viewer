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

    public Task<bool> ExistsAsync(string paymentId, CancellationToken ct = default) =>
        _db.ProcessedPayments.AnyAsync(x => x.PaymentId == paymentId, ct);

    public async Task<bool> TryRecordAsync(string paymentId, Guid userId, CancellationToken ct = default)
    {
        var subjectReference = await _db.Users
            .Where(x => x.Id == userId)
            .Select(x => (Guid?)x.PrivacySubjectId)
            .SingleOrDefaultAsync(ct);
        if (!subjectReference.HasValue)
            return false;

        // A duplicate is the only benign write failure. Do not acknowledge storage
        // failures as successful replays or poison the surrounding PostgreSQL transaction.
        var affected = await _db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO processed_payments (id, payment_id, subject_reference, processed_at)
            VALUES ({Guid.NewGuid()}, {paymentId}, {subjectReference.Value}, {DateTime.UtcNow})
            ON CONFLICT (payment_id) DO NOTHING
            """, ct);
        return affected == 1;
    }
}
