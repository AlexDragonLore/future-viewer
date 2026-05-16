using FutureViewer.DomainServices.Interfaces;
using FutureViewer.Infrastructure.AI;
using FutureViewer.Infrastructure.Auth;
using FutureViewer.Infrastructure.BackgroundServices;
using FutureViewer.Infrastructure.Email;
using FutureViewer.Infrastructure.Payment;
using FutureViewer.Infrastructure.Persistence;
using FutureViewer.Infrastructure.Persistence.Repositories;
using FutureViewer.Infrastructure.Telegram;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Telegram.Bot.Polling;

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
        services.Configure<TelegramOptions>(configuration.GetSection(TelegramOptions.SectionName));
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));

        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured");

        services.AddDbContext<AppDbContext>(opt =>
            opt.UseNpgsql(connectionString, npg => npg.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

        services.AddScoped<IReadingRepository, ReadingRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICardDeck, CardDeckRepository>();
        services.AddScoped<IProcessedPaymentRepository, ProcessedPaymentRepository>();
        services.AddScoped<IFeedbackRepository, FeedbackRepository>();
        services.AddScoped<IAchievementRepository, AchievementRepository>();
        services.AddScoped<ILeaderboardRepository, LeaderboardRepository>();
        services.AddScoped<IUserMemoryRepository, UserMemoryRepository>();
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
        services.AddSingleton<IAIMemoryExtractor, MemoryExtractionInterpreter>();
        services.AddSingleton<IFeedbackScorer, FeedbackScoringInterpreter>();
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<IEmailLinkBuilder, EmailLinkBuilder>();

        var paymentProvider = configuration.GetSection(PaymentOptions.SectionName).Get<PaymentOptions>()?.Provider;
        if (string.Equals(paymentProvider, "YooMoney", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IPaymentProvider, YooMoneyRedirectPaymentProvider>();
        else
            services.AddHttpClient<IPaymentProvider, YukassaClient>();

        AddTelegram(services);

        return services;
    }

    private static void AddTelegram(IServiceCollection services)
    {
        services.TryAddSingleton<ITelegramLinkUrlBuilder, TelegramLinkUrlBuilder>();
        services.AddSingleton<TelegramBotClientProvider>();
        services.AddSingleton<TelegramBotService>();
        services.AddSingleton<ITelegramNotifier>(sp => sp.GetRequiredService<TelegramBotService>());
        services.AddSingleton<IUpdateHandler, TelegramUpdateHandler>();

        services.AddScoped<FeedbackNotificationProcessor>();
        services.AddHostedService<TelegramPollingHostedService>();
        services.AddHostedService<FeedbackNotificationJob>();
    }
}
