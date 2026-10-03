using FutureViewer.Domain.Entities;
using FutureViewer.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace FutureViewer.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<TarotCard> TarotCards => Set<TarotCard>();
    public DbSet<DeckVariant> DeckVariants => Set<DeckVariant>();
    public DbSet<Reading> Readings => Set<Reading>();
    public DbSet<ReadingCard> ReadingCards => Set<ReadingCard>();
    public DbSet<ProcessedPayment> ProcessedPayments => Set<ProcessedPayment>();
    public DbSet<PaymentOrder> PaymentOrders => Set<PaymentOrder>();
    public DbSet<ReadingFeedback> ReadingFeedbacks => Set<ReadingFeedback>();
    public DbSet<Achievement> Achievements => Set<Achievement>();
    public DbSet<UserAchievement> UserAchievements => Set<UserAchievement>();
    public DbSet<UserMemoryRule> UserMemoryRules => Set<UserMemoryRule>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<AnnouncementRead> AnnouncementReads => Set<AnnouncementRead>();
    public DbSet<LegalDocument> LegalDocuments => Set<LegalDocument>();
    public DbSet<UserConsent> UserConsents => Set<UserConsent>();
    public DbSet<DataSubjectRequest> DataSubjectRequests => Set<DataSubjectRequest>();
    public DbSet<DataDeletionJob> DataDeletionJobs => Set<DataDeletionJob>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new TarotCardConfiguration());
        modelBuilder.ApplyConfiguration(new DeckVariantConfiguration());
        modelBuilder.ApplyConfiguration(new ReadingConfiguration());
        modelBuilder.ApplyConfiguration(new ReadingCardConfiguration());
        modelBuilder.ApplyConfiguration(new ProcessedPaymentConfiguration());
        modelBuilder.ApplyConfiguration(new PaymentOrderConfiguration());
        modelBuilder.ApplyConfiguration(new ReadingFeedbackConfiguration());
        modelBuilder.ApplyConfiguration(new AchievementConfiguration());
        modelBuilder.ApplyConfiguration(new UserAchievementConfiguration());
        modelBuilder.ApplyConfiguration(new UserMemoryRuleConfiguration());
        modelBuilder.ApplyConfiguration(new AnnouncementConfiguration());
        modelBuilder.ApplyConfiguration(new AnnouncementReadConfiguration());
        modelBuilder.ApplyConfiguration(new LegalDocumentConfiguration());
        modelBuilder.ApplyConfiguration(new UserConsentConfiguration());
        modelBuilder.ApplyConfiguration(new DataSubjectRequestConfiguration());
        modelBuilder.ApplyConfiguration(new DataDeletionJobConfiguration());
        modelBuilder.ApplyConfiguration(new AuditEventConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
