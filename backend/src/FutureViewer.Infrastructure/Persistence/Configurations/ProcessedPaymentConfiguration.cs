using FutureViewer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FutureViewer.Infrastructure.Persistence.Configurations;

public sealed class ProcessedPaymentConfiguration : IEntityTypeConfiguration<ProcessedPayment>
{
    public void Configure(EntityTypeBuilder<ProcessedPayment> b)
    {
        b.ToTable("processed_payments");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.PaymentId).HasColumnName("payment_id").IsRequired().HasMaxLength(128);
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.ProcessedAt).HasColumnName("processed_at");
        b.HasIndex(x => x.PaymentId).IsUnique();
    }
}
