using FutureViewer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FutureViewer.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.PrivacySubjectId).HasColumnName("privacy_subject_id").IsRequired();
        b.HasIndex(x => x.PrivacySubjectId).IsUnique();
        b.Property(x => x.Email).HasColumnName("email").IsRequired().HasMaxLength(256);
        b.HasIndex(x => x.Email).IsUnique();
        b.Property(x => x.PasswordHash).HasColumnName("password_hash").IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.FirstName).HasColumnName("first_name").HasMaxLength(80);
        b.Property(x => x.LastName).HasColumnName("last_name").HasMaxLength(80);
        b.Property(x => x.BirthYear).HasColumnName("birth_year");
        b.Property(x => x.IsAdultConfirmed)
            .HasColumnName("is_adult_confirmed")
            .IsRequired()
            .HasDefaultValue(false);
        b.Property(x => x.HistoryEnabled)
            .HasColumnName("history_enabled")
            .IsRequired()
            .HasDefaultValue(false);
        b.Property(x => x.AccountStatus)
            .HasColumnName("account_status")
            .HasConversion<int>()
            .IsRequired()
            .HasDefaultValue(Domain.Enums.UserAccountStatus.Active);
        b.Property(x => x.SecurityVersion)
            .HasColumnName("security_version")
            .IsRequired()
            .HasDefaultValue(1);
        b.Property(x => x.AccountDeletionRequestedAt).HasColumnName("account_deletion_requested_at");

        b.Property(x => x.IsAdmin)
            .HasColumnName("is_admin")
            .IsRequired()
            .HasDefaultValue(false);

        b.Property(x => x.IsEmailVerified)
            .HasColumnName("is_email_verified")
            .IsRequired()
            .HasDefaultValue(false);
        b.Property(x => x.EmailVerificationToken)
            .HasColumnName("email_verification_token")
            .HasMaxLength(128);
        b.HasIndex(x => x.EmailVerificationToken);
        b.Property(x => x.EmailVerificationSentAt)
            .HasColumnName("email_verification_sent_at");

        b.Property(x => x.PasswordResetToken)
            .HasColumnName("password_reset_token")
            .HasMaxLength(128);
        b.HasIndex(x => x.PasswordResetToken);
        b.Property(x => x.PasswordResetTokenExpiresAt)
            .HasColumnName("password_reset_token_expires_at");

        b.Property(x => x.SubscriptionStatus)
            .HasColumnName("subscription_status")
            .HasConversion<int>();
        b.Property(x => x.SubscriptionExpiresAt).HasColumnName("subscription_expires_at");
        b.Property(x => x.YukassaSubscriptionId)
            .HasColumnName("yukassa_subscription_id")
            .HasMaxLength(128);


        b.HasMany(x => x.Readings)
            .WithOne(x => x.User)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.MemoryRules)
            .WithOne(x => x.User)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.Consents)
            .WithOne(x => x.User)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
