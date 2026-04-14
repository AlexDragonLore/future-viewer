using FutureViewer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FutureViewer.Infrastructure.Persistence.Configurations;

public sealed class ReadingConfiguration : IEntityTypeConfiguration<Reading>
{
    public void Configure(EntityTypeBuilder<Reading> b)
    {
        b.ToTable("readings");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.SpreadType).HasColumnName("spread_type").HasConversion<int>();
        b.Property(x => x.Question).HasColumnName("question").HasMaxLength(1000);
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.AiInterpretation).HasColumnName("ai_interpretation");
        b.Property(x => x.AiModel).HasColumnName("ai_model").HasMaxLength(64);

        b.HasMany(x => x.Cards)
            .WithOne(x => x.Reading)
            .HasForeignKey(x => x.ReadingId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.UserId, x.CreatedAt });
    }
}
