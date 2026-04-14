using FutureViewer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FutureViewer.Infrastructure.Persistence.Configurations;

public sealed class ReadingCardConfiguration : IEntityTypeConfiguration<ReadingCard>
{
    public void Configure(EntityTypeBuilder<ReadingCard> b)
    {
        b.ToTable("reading_cards");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ReadingId).HasColumnName("reading_id");
        b.Property(x => x.CardId).HasColumnName("card_id");
        b.Property(x => x.Position).HasColumnName("position");
        b.Property(x => x.IsReversed).HasColumnName("is_reversed");

        b.HasOne(x => x.Card)
            .WithMany()
            .HasForeignKey(x => x.CardId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
