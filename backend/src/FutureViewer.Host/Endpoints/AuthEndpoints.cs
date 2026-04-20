using FluentValidation;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Services;

namespace FutureViewer.Host.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuth(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", async (
            RegisterRequest request,
            IValidator<RegisterRequest> validator,
            AuthService service,
            CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            var response = await service.RegisterAsync(request, ct);
            return Results.Accepted($"/api/users/{response.UserId}", response);
        });

        group.MapPost("/login", async (
            LoginRequest request,
            IValidator<LoginRequest> validator,
            AuthService service,
            CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            var response = await service.LoginAsync(request, ct);
            return Results.Ok(response);
        });

        group.MapPost("/verify-email", async (
            VerifyEmailRequest request,
            AuthService service,
            CancellationToken ct) =>
        {
            var response = await service.VerifyEmailAsync(request.Token, ct);
            return Results.Ok(response);
        });

        group.MapPost("/resend-verification", async (
            ResendVerificationRequest request,
            IValidator<ResendVerificationRequest> validator,
            AuthService service,
            CancellationToken ct) =>
        {
            await validator.ValidateAndThrowAsync(request, ct);
            await service.ResendVerificationAsync(request, ct);
            return Results.NoContent();
        });

        return app;
    }
}
