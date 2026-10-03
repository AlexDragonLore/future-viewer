using FutureViewer.DomainServices;
using FutureViewer.DomainServices.Interfaces;
using FutureViewer.Infrastructure.AI;
using FutureViewer.Infrastructure.Auth;
using FutureViewer.Infrastructure.BackgroundServices;
using FutureViewer.Infrastructure.Compliance;
using FutureViewer.Infrastructure.Email;
using FutureViewer.Infrastructure.Payment;
using FutureViewer.Infrastructure.Persistence;
using FutureViewer.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FutureViewer.Infrastructure.DependencyInjection;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool useDevelopmentAiFallbacks = false)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<AIOptions>(configuration.GetSection(AIOptions.SectionName));
        services.Configure<OpenAIOptions>(configuration.GetSection(OpenAIOptions.SectionName));
        services.Configure<DeepSeekOptions>(configuration.GetSection(DeepSeekOptions.SectionName));
        services.Configure<PaymentOptions>(configuration.GetSection(PaymentOptions.SectionName));
        services.Configure<YukassaOptions>(configuration.GetSection(YukassaOptions.SectionName));
        services.Configure<YooMoneyOptions>(configuration.GetSection(YooMoneyOptions.SectionName));
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.AddSingleton(configuration.GetSection(PrivacyOptions.SectionName).Get<PrivacyOptions>() ?? new PrivacyOptions());
        services.AddSingleton<IProcessorRegistryGuard, ProcessorRegistryGuard>();

        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured");

        services.AddDbContext<AppDbContext>(opt =>
            opt.UseNpgsql(connectionString, npg => npg.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

        services.AddScoped<IReadingRepository, ReadingRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICardDeck, CardDeckRepository>();
        services.AddScoped<IProcessedPaymentRepository, ProcessedPaymentRepository>();
        services.AddScoped<IPaymentOrderRepository, PaymentOrderRepository>();
        services.AddScoped<IFeedbackRepository, FeedbackRepository>();
        services.AddScoped<IAchievementRepository, AchievementRepository>();
        services.AddScoped<ILeaderboardRepository, LeaderboardRepository>();
        services.AddScoped<IUserMemoryRepository, UserMemoryRepository>();
        services.AddScoped<IAnnouncementRepository, AnnouncementRepository>();
        services.AddScoped<IPrivacyRepository, PrivacyRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<AIChatClientFactory>();
        services.AddSingleton<IAIInterpreter, OpenAIInterpreter>();
        var aiProvider = configuration.GetSection(AIOptions.SectionName)[nameof(AIOptions.Provider)] ?? "OpenAI";
        var hasConfiguredAiKey = aiProvider.Equals("DeepSeek", StringComparison.OrdinalIgnoreCase)
            ? !string.IsNullOrWhiteSpace(configuration.GetSection(DeepSeekOptions.SectionName)[nameof(DeepSeekOptions.ApiKey)])
            : !string.IsNullOrWhiteSpace(configuration.GetSection(OpenAIOptions.SectionName)[nameof(OpenAIOptions.ApiKey)]);
        if (useDevelopmentAiFallbacks && !hasConfiguredAiKey)
            services.AddSingleton<IAIQuestionValidator, DevelopmentQuestionValidator>();
        else
            services.AddSingleton<IAIQuestionValidator, QuestionValidationInterpreter>();
        // These secondary AI uses are disabled by default. They previously sent raw
        // question/answer/self-report content to the provider without separate consent.
        services.AddSingleton<IAIMemoryExtractor, DisabledMemoryExtractor>();
        services.AddSingleton<IFeedbackScorer, PrivacyPreservingFeedbackScorer>();
        var emailOptions = configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>() ?? new EmailOptions();
        if (emailOptions.GetTransport() == "RegruWebmail")
        {
            if (!emailOptions.IsConfigured)
                throw new InvalidOperationException("Email:RegruWebmail requires mailbox credentials and a matching From address.");
            services.AddSingleton<IEmailSender, RegruWebmailEmailSender>();
        }
        else
            services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<IEmailLinkBuilder, EmailLinkBuilder>();

        var paymentOptions = configuration.GetSection(PaymentOptions.SectionName).Get<PaymentOptions>()
                             ?? new PaymentOptions();
        if (!paymentOptions.Enabled)
            services.AddSingleton<IPaymentProvider, DisabledPaymentProvider>();
        else if (string.Equals(paymentOptions.Provider, "YooMoney", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IPaymentProvider, YooMoneyRedirectPaymentProvider>();
        else
            services.AddHttpClient<IPaymentProvider, YukassaClient>();

        services.AddScoped<AccountDeletionProcessor>();
        services.AddHostedService<AccountDeletionJob>();
        services.AddHostedService<DataSubjectRequestDeadlineJob>();
        services.AddHostedService<RetentionCleanupJob>();

        return services;
    }

}
