using FutureViewer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FutureViewer.Infrastructure.Persistence.Configurations;

public sealed class DataSubjectRequestConfiguration : IEntityTypeConfiguration<DataSubjectRequest>
{
    public void Configure(EntityTypeBuilder<DataSubjectRequest> b)
    {
        b.ToTable("data_subject_requests");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.SubjectReference).HasColumnName("subject_reference").IsRequired();
        b.Property(x => x.RequestType).HasColumnName("request_type").HasConversion<int>();
        b.Property(x => x.ReceivedAt).HasColumnName("received_at");
        b.Property(x => x.IdentityVerifiedAt).HasColumnName("identity_verified_at");
        b.Property(x => x.DueAt).HasColumnName("due_at");
        b.Property(x => x.Status).HasColumnName("status").HasConversion<int>();
        b.Property(x => x.CompletedAt).HasColumnName("completed_at");
        b.Property(x => x.ResultReference).HasColumnName("result_reference").HasMaxLength(128);
        b.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => x.SubjectReference);
        b.HasIndex(x => new { x.Status, x.DueAt });
    }
}
