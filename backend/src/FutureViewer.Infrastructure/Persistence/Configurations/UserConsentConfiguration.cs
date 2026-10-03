using FutureViewer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FutureViewer.Infrastructure.Persistence.Configurations;

public sealed class UserConsentConfiguration : IEntityTypeConfiguration<UserConsent>
{
    public void Configure(EntityTypeBuilder<UserConsent> b)
    {
        b.ToTable("user_consents");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.SubjectReference).HasColumnName("subject_reference").IsRequired();
        b.Property(x => x.ConsentType).HasColumnName("consent_type").HasConversion<int>();
        b.Property(x => x.LegalDocumentId).HasColumnName("legal_document_id");
        b.Property(x => x.DocumentVersion).HasColumnName("document_version").IsRequired().HasMaxLength(64);
        b.Property(x => x.ContentHash).HasColumnName("content_hash").IsRequired().HasMaxLength(64);
        b.Property(x => x.AcceptedAt).HasColumnName("accepted_at");
        b.Property(x => x.RevokedAt).HasColumnName("revoked_at");
        b.Property(x => x.CollectionSource).HasColumnName("collection_source").IsRequired().HasMaxLength(64);
        b.Property(x => x.IpHash).HasColumnName("ip_hash").HasMaxLength(64);
        b.Property(x => x.UserAgentHash).HasColumnName("user_agent_hash").HasMaxLength(64);
        b.HasOne(x => x.LegalDocument)
            .WithMany(x => x.Consents)
            .HasForeignKey(x => x.LegalDocumentId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.UserId, x.ConsentType })
            .IsUnique()
            // A new purchase needs its own contractual acceptance evidence. Privacy
            // permissions still have only one active record per user and purpose.
            .HasFilter("revoked_at IS NULL AND user_id IS NOT NULL AND consent_type <> 1");
        b.HasIndex(x => x.SubjectReference);
        b.HasIndex(x => x.LegalDocumentId);
    }
}
