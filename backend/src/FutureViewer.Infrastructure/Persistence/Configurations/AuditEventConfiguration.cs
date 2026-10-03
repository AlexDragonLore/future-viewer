using FutureViewer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FutureViewer.Infrastructure.Persistence.Configurations;

public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> b)
    {
        b.ToTable("audit_events");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.OccurredAt).HasColumnName("occurred_at");
        b.Property(x => x.EventType).HasColumnName("event_type").IsRequired().HasMaxLength(64);
        b.Property(x => x.CorrelationId).HasColumnName("correlation_id").IsRequired().HasMaxLength(64);
        b.Property(x => x.ActorSubjectReference).HasColumnName("actor_subject_reference");
        b.Property(x => x.TargetType).HasColumnName("target_type").IsRequired().HasMaxLength(64);
        b.Property(x => x.TargetReference).HasColumnName("target_reference");
        b.Property(x => x.Outcome).HasColumnName("outcome").IsRequired().HasMaxLength(32);
        b.Property(x => x.ReasonCode).HasColumnName("reason_code").HasMaxLength(64);
        b.Property(x => x.DocumentVersion).HasColumnName("document_version").HasMaxLength(64);
        b.HasIndex(x => x.OccurredAt);
        b.HasIndex(x => new { x.EventType, x.OccurredAt });
        b.HasIndex(x => x.ActorSubjectReference);
    }
}
