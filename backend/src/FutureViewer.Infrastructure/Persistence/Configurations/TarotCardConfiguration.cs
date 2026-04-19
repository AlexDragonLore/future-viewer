using System.Text.Json;
using FutureViewer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FutureViewer.Infrastructure.Persistence.Configurations;

public sealed class TarotCardConfiguration : IEntityTypeConfiguration<TarotCard>
{
    public void Configure(EntityTypeBuilder<TarotCard> b)
    {
        b.ToTable("tarot_cards");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.Name).HasColumnName("name").IsRequired().HasMaxLength(128);
        b.Property(x => x.Suit).HasColumnName("suit").HasConversion<int>();
        b.Property(x => x.Number).HasColumnName("number");
        b.Property(x => x.DescriptionUpright).HasColumnName("description_upright").HasMaxLength(2000);
        b.Property(x => x.DescriptionReversed).HasColumnName("description_reversed").HasMaxLength(2000);
        b.Property(x => x.ImagePath).HasColumnName("image_path").HasMaxLength(256);

        b.Property(x => x.NameEn)
            .HasColumnName("name_en")
            .IsRequired()
            .HasMaxLength(128)
            .HasDefaultValue(string.Empty);

        b.Property(x => x.ShortUpright)
            .HasColumnName("short_upright")
            .IsRequired()
            .HasMaxLength(512)
            .HasDefaultValue(string.Empty);

        b.Property(x => x.ShortReversed)
            .HasColumnName("short_reversed")
            .IsRequired()
            .HasMaxLength(512)
            .HasDefaultValue(string.Empty);

        b.Property(x => x.SuggestedTone)
            .HasColumnName("suggested_tone")
            .HasConversion<int>()
            .HasDefaultValue(Domain.Enums.SuggestedTone.Neutral);

        var stringListConverter = new ValueConverter<List<string>, string>(
            v => JsonSerializer.Serialize(v, JsonOpts),
            v => JsonSerializer.Deserialize<List<string>>(v, JsonOpts) ?? new List<string>());

        var stringListComparer = new ValueComparer<List<string>>(
            (a, c) => (a == null && c == null) || (a != null && c != null && a.SequenceEqual(c)),
            v => v == null ? 0 : v.Aggregate(0, (h, s) => HashCode.Combine(h, s.GetHashCode())),
            v => v.ToList());

        var nullableStringListConverter = new ValueConverter<List<string>?, string?>(
            v => v == null ? null : JsonSerializer.Serialize(v, JsonOpts),
            v => v == null ? null : JsonSerializer.Deserialize<List<string>>(v, JsonOpts));

        var nullableStringListComparer = new ValueComparer<List<string>?>(
            (a, c) => (a == null && c == null) || (a != null && c != null && a.SequenceEqual(c)),
            v => v == null ? 0 : v.Aggregate(0, (h, s) => HashCode.Combine(h, s.GetHashCode())),
            v => v == null ? null : v.ToList());

        b.Property(x => x.UprightKeywords)
            .HasColumnName("upright_keywords")
            .HasColumnType("jsonb")
            .HasConversion(stringListConverter, stringListComparer)
            .HasDefaultValueSql("'[]'::jsonb");

        b.Property(x => x.ReversedKeywords)
            .HasColumnName("reversed_keywords")
            .HasColumnType("jsonb")
            .HasConversion(stringListConverter, stringListComparer)
            .HasDefaultValueSql("'[]'::jsonb");

        b.Property(x => x.Aliases)
            .HasColumnName("aliases")
            .HasColumnType("jsonb")
            .HasConversion(nullableStringListConverter, nullableStringListComparer);

        b.HasMany(x => x.DeckVariants)
            .WithOne(x => x.Card!)
            .HasForeignKey(x => x.CardId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
