using FutureViewer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FutureViewer.Infrastructure.Persistence.Configurations;

public sealed class ReadingFeedbackConfiguration : IEntityTypeConfiguration<ReadingFeedback>
{
    public void Configure(EntityTypeBuilder<ReadingFeedback> b)
    {
        b.ToTable("reading_feedbacks");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ReadingId).HasColumnName("reading_id");
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.Token).HasColumnName("token").IsRequired().HasMaxLength(64);
        b.Property(x => x.SelfReport).HasColumnName("self_report").HasMaxLength(4000);
        b.Property(x => x.AiScore).HasColumnName("ai_score");
        b.Property(x => x.AiScoreReason).HasColumnName("ai_score_reason").HasMaxLength(2000);
        b.Property(x => x.IsSincere).HasColumnName("is_sincere");
        b.Property(x => x.ScheduledAt).HasColumnName("scheduled_at");
        b.Property(x => x.NotifiedAt).HasColumnName("notified_at");
        b.Property(x => x.AnsweredAt).HasColumnName("answered_at");
        b.Property(x => x.Status).HasColumnName("status").HasConversion<int>();
        b.Property(x => x.CreatedAt).HasColumnName("created_at");

        b.HasOne(x => x.Reading)
            .WithOne(x => x.Feedback)
            .HasForeignKey<ReadingFeedback>(x => x.ReadingId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.User)
            .WithMany(x => x.Feedbacks)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.ReadingId).IsUnique();
        b.HasIndex(x => x.Token).IsUnique();
        b.HasIndex(x => new { x.UserId, x.Status });
        b.HasIndex(x => x.ScheduledAt);
    }
}
