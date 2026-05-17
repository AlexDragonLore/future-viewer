using FutureViewer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FutureViewer.Infrastructure.Persistence.Configurations;

public sealed class TarotPlusSessionConfiguration : IEntityTypeConfiguration<TarotPlusSession>
{
    public void Configure(EntityTypeBuilder<TarotPlusSession> b)
    {
        b.ToTable("tarot_plus_sessions");

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");

        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.Status).HasColumnName("status").HasConversion<int>();
        b.Property(x => x.Route).HasColumnName("route").HasConversion<int>();

        b.Property(x => x.CoreRequest).HasColumnName("core_request").HasMaxLength(4000);
        b.Property(x => x.PreviewText).HasColumnName("preview_text").HasMaxLength(4000);

        b.Property(x => x.AnswersJson).HasColumnName("answers_json").HasColumnType("jsonb");
        b.Property(x => x.SelectedSpreadsJson).HasColumnName("selected_spreads_json").HasColumnType("jsonb");
        b.Property(x => x.DrawnCardsJson).HasColumnName("drawn_cards_json").HasColumnType("jsonb");
        b.Property(x => x.FollowUpsJson).HasColumnName("followups_json").HasColumnType("jsonb");
        b.Property(x => x.SafetyFlagsJson).HasColumnName("safety_flags_json").HasColumnType("jsonb");

        b.Property(x => x.ReportMarkdown).HasColumnName("report_markdown");
        b.Property(x => x.AiModel).HasColumnName("ai_model").HasMaxLength(128);

        b.Property(x => x.FollowUpsLeft).HasColumnName("followups_left");
        b.Property(x => x.PaymentId).HasColumnName("payment_id").HasMaxLength(128);
        b.Property(x => x.PriceRub).HasColumnName("price_rub").HasPrecision(10, 2);
        b.Property(x => x.PaidAt).HasColumnName("paid_at");

        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.ExpiresAt).HasColumnName("expires_at");

        b.HasIndex(x => x.UserId);
        b.HasIndex(x => x.PaymentId);
        b.HasIndex(x => new { x.UserId, x.CreatedAt });

        b.HasOne(x => x.User)
            .WithMany(x => x.TarotPlusSessions)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
