using FutureViewer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FutureViewer.Infrastructure.Persistence.Configurations;

public sealed class PaymentOrderConfiguration : IEntityTypeConfiguration<PaymentOrder>
{
    public void Configure(EntityTypeBuilder<PaymentOrder> b)
    {
        b.ToTable("payment_orders");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.PublicId).HasColumnName("public_id").IsRequired();
        b.HasIndex(x => x.PublicId).IsUnique();
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);
        b.Property(x => x.SubjectReference).HasColumnName("subject_reference").IsRequired();
        b.HasIndex(x => x.SubjectReference);
        b.Property(x => x.TariffCode).HasColumnName("tariff_code").HasMaxLength(64).IsRequired();
        b.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 2).IsRequired();
        b.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        b.Property(x => x.AccessDays).HasColumnName("access_days").IsRequired();
        b.Property(x => x.Provider).HasColumnName("provider").HasMaxLength(64).IsRequired();
        b.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(64).IsRequired();
        b.HasIndex(x => new { x.Provider, x.IdempotencyKey }).IsUnique();
        b.Property(x => x.ProviderPaymentId).HasColumnName("provider_payment_id").HasMaxLength(200);
        b.HasIndex(x => x.ProviderPaymentId).IsUnique();
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired();
        b.Property(x => x.ReceiptStatus).HasColumnName("receipt_status").HasConversion<string>().HasMaxLength(32).IsRequired();
        b.Property(x => x.ReceiptReference).HasColumnName("receipt_reference").HasMaxLength(200);
        b.Property(x => x.ReceiptIssuedAt).HasColumnName("receipt_issued_at");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(x => x.CompletedAt).HasColumnName("completed_at");
    }
}
