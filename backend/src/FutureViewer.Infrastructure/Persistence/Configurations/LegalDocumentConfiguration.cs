using FutureViewer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FutureViewer.Infrastructure.Persistence.Configurations;

public sealed class LegalDocumentConfiguration : IEntityTypeConfiguration<LegalDocument>
{
    public void Configure(EntityTypeBuilder<LegalDocument> b)
    {
        b.ToTable("legal_documents");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.DocumentType).HasColumnName("document_type").HasConversion<int>();
        b.Property(x => x.Version).HasColumnName("version").IsRequired().HasMaxLength(64);
        b.Property(x => x.ContentHash).HasColumnName("content_hash").IsRequired().HasMaxLength(64);
        b.Property(x => x.PublishedAt).HasColumnName("published_at");
        b.Property(x => x.EffectiveAt).HasColumnName("effective_at");
        b.Property(x => x.IsActive).HasColumnName("is_active").IsRequired().HasDefaultValue(false);
        b.HasIndex(x => new { x.DocumentType, x.Version }).IsUnique();
        b.HasIndex(x => x.DocumentType).IsUnique().HasFilter("is_active");
    }
}
