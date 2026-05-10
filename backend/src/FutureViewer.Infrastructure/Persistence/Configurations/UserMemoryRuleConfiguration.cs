using FutureViewer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FutureViewer.Infrastructure.Persistence.Configurations;

public sealed class UserMemoryRuleConfiguration : IEntityTypeConfiguration<UserMemoryRule>
{
    public void Configure(EntityTypeBuilder<UserMemoryRule> b)
    {
        b.ToTable("user_memory_rules");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.Text).HasColumnName("text").IsRequired().HasMaxLength(500);
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        b.HasIndex(x => new { x.UserId, x.CreatedAt });
    }
}
