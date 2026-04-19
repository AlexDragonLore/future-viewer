using FutureViewer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FutureViewer.Infrastructure.Persistence.Configurations;

public sealed class DeckVariantConfiguration : IEntityTypeConfiguration<DeckVariant>
{
    public void Configure(EntityTypeBuilder<DeckVariant> b)
    {
        b.ToTable("deck_variants");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        b.Property(x => x.CardId).HasColumnName("card_id").IsRequired();
        b.Property(x => x.DeckType).HasColumnName("deck_type").HasConversion<int>().IsRequired();
        b.Property(x => x.VariantNote).HasColumnName("variant_note").IsRequired().HasMaxLength(1000);

        b.HasIndex(x => new { x.CardId, x.DeckType }).IsUnique();
    }
}
