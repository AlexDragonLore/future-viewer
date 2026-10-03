using FutureViewer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FutureViewer.Infrastructure.Persistence.Configurations;

public sealed class DataDeletionJobConfiguration : IEntityTypeConfiguration<DataDeletionJob>
{
    public void Configure(EntityTypeBuilder<DataDeletionJob> b)
    {
        b.ToTable("data_deletion_jobs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.SubjectReference).HasColumnName("subject_reference").IsRequired();
        b.Property(x => x.RequestedAt).HasColumnName("requested_at");
        b.Property(x => x.ScheduledAt).HasColumnName("scheduled_at");
        b.Property(x => x.StartedAt).HasColumnName("started_at");
        b.Property(x => x.CompletedAt).HasColumnName("completed_at");
        b.Property(x => x.Status).HasColumnName("status").HasConversion<int>();
        b.Property(x => x.FailureReason).HasColumnName("failure_reason").HasMaxLength(64);
        b.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => x.SubjectReference);
        b.HasIndex(x => new { x.Status, x.ScheduledAt });
    }
}
