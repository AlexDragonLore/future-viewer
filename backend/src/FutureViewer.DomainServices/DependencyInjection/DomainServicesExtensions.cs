using FluentValidation;
using FutureViewer.DomainServices.Interfaces;
using FutureViewer.DomainServices.Services;
using FutureViewer.DomainServices.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace FutureViewer.DomainServices.DependencyInjection;

public static class DomainServicesExtensions
{
    public static IServiceCollection AddDomainServices(this IServiceCollection services)
    {
        services.AddSingleton<IAiPrivacyGateway, AiPrivacyGateway>();
        services.AddScoped<CardDeckService>();
        services.AddScoped<InterpretationService>();
        services.AddScoped<ReadingService>();
        services.AddScoped<AuthService>();
        services.AddScoped<SubscriptionService>();
        services.AddScoped<FeedbackService>();
        services.AddScoped<AchievementService>();
        services.AddScoped<LeaderboardService>();
        services.AddScoped<AdminService>();
        services.AddScoped<PersonalizationService>();
        services.AddScoped<AnnouncementService>();
        services.AddScoped<PrivacyService>();

        services.AddValidatorsFromAssemblyContaining<CreateReadingRequestValidator>();
        return services;
    }
}
