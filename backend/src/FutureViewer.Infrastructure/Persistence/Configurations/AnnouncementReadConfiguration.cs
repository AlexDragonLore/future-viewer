using FutureViewer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FutureViewer.Infrastructure.Persistence.Configurations;

public sealed class AnnouncementReadConfiguration : IEntityTypeConfiguration<AnnouncementRead>
{
    public void Configure(EntityTypeBuilder<AnnouncementRead> b)
    {
        b.ToTable("announcement_reads");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.AnnouncementId).HasColumnName("announcement_id");
        b.Property(x => x.ReadAt).HasColumnName("read_at");

        b.HasOne(x => x.User)
            .WithMany(x => x.AnnouncementReads)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.AnnouncementId);
        b.HasIndex(x => new { x.UserId, x.AnnouncementId }).IsUnique();
    }
}
