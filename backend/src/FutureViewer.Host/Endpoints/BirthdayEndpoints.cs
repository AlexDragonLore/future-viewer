using FutureViewer.DomainServices.Services;
using FutureViewer.Host.Auth;

namespace FutureViewer.Host.Endpoints;

public static class BirthdayEndpoints
{
    public static IEndpointRouteBuilder MapBirthday(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/birthday").WithTags("Birthday").ExcludeFromDescription();
        group.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers["X-Robots-Tag"] = "noindex, nofollow, noarchive";
            return await next(context);
        });

        group.MapPost("/unlock", (BirthdayUnlockRequest request, BirthdayAccess access) =>
        {
            if (!access.Configured)
                return Results.Json(new { message = "Приглашение ещё готовится. Попробуй зайти чуть позже." }, statusCode: 503);
            return access.Accepts(request.Key)
                ? Results.Ok(access.Issue(0))
                : Results.Json(new { message = "Ключ не подошёл. Проверь его и попробуй ещё раз." }, statusCode: 403);
        }).RequireRateLimiting("birthday-unlock");

        group.MapPost("/session", (BirthdayTicketRequest request, BirthdayAccess access) =>
        {
            var state = access.Read(request.Ticket, out _);
            return state is null ? Expired() : Results.Ok(access.Describe(request.Ticket!, state.Completed));
        });

        group.MapPost("/answer", (BirthdayAnswerRequest request, BirthdayAccess access) =>
        {
            var state = access.Read(request.Ticket, out var expiresAt);
            if (state is null) return Expired();
            var question = BirthdayGame.Question(state.Completed);
            if (question is null || question.Id != request.QuestionId
                || !question.Options.Any(option => option.Id == request.OptionId))
                return Results.BadRequest(new { message = "Этот этап уже изменился. Обнови страницу, чтобы продолжить." });
            var correct = BirthdayGame.IsCorrect(state.Completed, request.OptionId);
            var session = correct
                ? access.Issue(state.Completed + 1, expiresAt)
                : access.Describe(request.Ticket!, state.Completed);
            return Results.Ok(new { correct, message = BirthdayGame.Feedback(state.Completed, correct), session });
        });

        group.MapPost("/gift", (BirthdayTicketRequest request, BirthdayAccess access) =>
        {
            var state = access.Read(request.Ticket, out _);
            if (state is null) return Expired();
            return state.Completed == BirthdayGame.TotalStages
                ? Results.Ok(new { amount = BirthdayGame.GiftAmount, recipient = "Даша", age = 26 })
                : Results.Json(new { message = "Подарок откроется, когда ты соберёшь все пять арканов." }, statusCode: 403);
        });

        group.MapPost("/assets/{kind}", (string kind, BirthdayTicketRequest request, BirthdayAccess access) =>
        {
            var state = access.Read(request.Ticket, out _);
            if (state is null) return Expired();
            if (state.Completed != BirthdayGame.TotalStages)
                return Results.Json(new { message = "Сначала заверши игру — подарок уже ждёт." }, statusCode: 403);
            var path = access.AssetPath(kind);
            return path is not null && File.Exists(path)
                ? Results.File(path, "image/jpeg", $"birthday-{kind}.jpg", enableRangeProcessing: false)
                : Results.NotFound(new { message = "Не удалось найти открытку. Попробуй чуть позже." });
        });

        return app;
    }

    private static IResult Expired() => Results.Json(
        new { error = "birthday_session_expired", message = "Введи секретный ключ, чтобы снова открыть приглашение." }, statusCode: 401);
}

public sealed record BirthdayUnlockRequest(string? Key);
public sealed record BirthdayTicketRequest(string? Ticket);
public sealed record BirthdayAnswerRequest(string? Ticket, string? QuestionId, string? OptionId);
