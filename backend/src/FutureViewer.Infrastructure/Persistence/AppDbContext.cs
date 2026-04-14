using FutureViewer.Domain.Entities;
using FutureViewer.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace FutureViewer.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<TarotCard> TarotCards => Set<TarotCard>();
    public DbSet<Reading> Readings => Set<Reading>();
    public DbSet<ReadingCard> ReadingCards => Set<ReadingCard>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new TarotCardConfiguration());
        modelBuilder.ApplyConfiguration(new ReadingConfiguration());
        modelBuilder.ApplyConfiguration(new ReadingCardConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
