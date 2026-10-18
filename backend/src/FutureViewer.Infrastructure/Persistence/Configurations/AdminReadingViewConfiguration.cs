using FutureViewer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FutureViewer.Infrastructure.Persistence.Configurations;

public sealed class AdminReadingViewConfiguration : IEntityTypeConfiguration<AdminReadingView>
{
    public void Configure(EntityTypeBuilder<AdminReadingView> b)
    {
        b.ToTable("admin_reading_views");
        b.HasKey(x => new { x.AdminId, x.ReadingId });
        b.Property(x => x.AdminId).HasColumnName("admin_id");
        b.Property(x => x.ReadingId).HasColumnName("reading_id");
        b.Property(x => x.ViewedAt).HasColumnName("viewed_at");
        b.HasOne<User>().WithMany().HasForeignKey(x => x.AdminId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Reading>().WithMany().HasForeignKey(x => x.ReadingId).OnDelete(DeleteBehavior.Cascade);
    }
}
