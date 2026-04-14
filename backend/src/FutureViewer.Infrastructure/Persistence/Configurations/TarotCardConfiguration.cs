using FutureViewer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FutureViewer.Infrastructure.Persistence.Configurations;

public sealed class TarotCardConfiguration : IEntityTypeConfiguration<TarotCard>
{
    public void Configure(EntityTypeBuilder<TarotCard> b)
    {
        b.ToTable("tarot_cards");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.Name).HasColumnName("name").IsRequired().HasMaxLength(128);
        b.Property(x => x.Suit).HasColumnName("suit").HasConversion<int>();
        b.Property(x => x.Number).HasColumnName("number");
        b.Property(x => x.DescriptionUpright).HasColumnName("description_upright").HasMaxLength(2000);
        b.Property(x => x.DescriptionReversed).HasColumnName("description_reversed").HasMaxLength(2000);
        b.Property(x => x.ImagePath).HasColumnName("image_path").HasMaxLength(256);
    }
}
