using FutureViewer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FutureViewer.Infrastructure.Persistence.Configurations;

public sealed class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
{
    public void Configure(EntityTypeBuilder<Announcement> b)
    {
        b.ToTable("announcements");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.Code).HasColumnName("code").IsRequired().HasMaxLength(64);
        b.Property(x => x.Title).HasColumnName("title").IsRequired().HasMaxLength(160);
        b.Property(x => x.Body).HasColumnName("body").IsRequired().HasMaxLength(2000);
        b.Property(x => x.PublishedAt).HasColumnName("published_at");
        b.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .IsRequired()
            .HasDefaultValue(true);

        b.HasMany(x => x.Reads)
            .WithOne(x => x.Announcement)
            .HasForeignKey(x => x.AnnouncementId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => x.PublishedAt);
    }
}
