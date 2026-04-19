using FluentValidation;
using FutureViewer.DomainServices.DTOs;
using FutureViewer.DomainServices.Exceptions;
using FutureViewer.DomainServices.Services;

namespace FutureViewer.Host.Endpoints;

public static class FeedbackEndpoints
{
    public static IEndpointRouteBuilder MapFeedbacks(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/feedbacks").WithTags("Feedbacks");

        group.MapGet("/my", async (
            FeedbackService service,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var userId = ctx.User.GetUserId()
                ?? throw new UnauthorizedException("Authentication required");
            var feedbacks = await service.GetUserFeedbacksAsync(userId, ct);
            return Results.Ok(feedbacks);
        }).RequireAuthorization();

        group.MapGet("/{token}", async (
            string token,
            FeedbackService service,
            CancellationToken ct) =>
        {
            var feedback = await service.GetByTokenAsync(token, ct);
            return Results.Ok(feedback);
        });

        group.MapPost("/{token}", async (
            string token,
            SubmitFeedbackBody body,
            IValidator<SubmitFeedbackRequest> validator,
            FeedbackService service,
            CancellationToken ct) =>
        {
            var request = new SubmitFeedbackRequest
            {
                Token = token,
                SelfReport = body?.SelfReport ?? string.Empty
            };
            await validator.ValidateAndThrowAsync(request, ct);
            var result = await service.SubmitAsync(token, request.SelfReport, ct);
            return Results.Ok(result);
        });

        return app;
    }

    public sealed class SubmitFeedbackBody
    {
        public string SelfReport { get; init; } = string.Empty;
    }
}
