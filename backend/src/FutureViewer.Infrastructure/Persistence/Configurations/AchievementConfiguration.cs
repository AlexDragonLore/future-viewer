using FutureViewer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FutureViewer.Infrastructure.Persistence.Configurations;

public sealed class AchievementConfiguration : IEntityTypeConfiguration<Achievement>
{
    public void Configure(EntityTypeBuilder<Achievement> b)
    {
        b.ToTable("achievements");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.Code).HasColumnName("code").IsRequired().HasMaxLength(64);
        b.Property(x => x.NameRu).HasColumnName("name_ru").IsRequired().HasMaxLength(128);
        b.Property(x => x.DescriptionRu).HasColumnName("description_ru").IsRequired().HasMaxLength(512);
        b.Property(x => x.IconPath).HasColumnName("icon_path").IsRequired().HasMaxLength(256);
        b.Property(x => x.SortOrder).HasColumnName("sort_order");
        b.Property(x => x.Points).HasColumnName("points").HasDefaultValue(0);

        b.HasIndex(x => x.Code).IsUnique();
    }
}
