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
        IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<OpenAIOptions>(configuration.GetSection(OpenAIOptions.SectionName));
        services.Configure<YukassaOptions>(configuration.GetSection(YukassaOptions.SectionName));
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
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IAIInterpreter, OpenAIInterpreter>();
        services.AddSingleton<IFeedbackScorer, FeedbackScoringInterpreter>();
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<IEmailLinkBuilder, EmailLinkBuilder>();

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
