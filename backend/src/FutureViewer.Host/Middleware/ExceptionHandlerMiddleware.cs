using FluentValidation;
using FutureViewer.DomainServices.Exceptions;

namespace FutureViewer.Host.Middleware;

public sealed class ExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlerMiddleware> _logger;

    public ExceptionHandlerMiddleware(RequestDelegate next, ILogger<ExceptionHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (ValidationException ex)
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            await ctx.Response.WriteAsJsonAsync(new
            {
                error = "validation_error",
                details = ex.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage })
            });
        }
        catch (NotFoundException ex)
        {
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            await ctx.Response.WriteAsJsonAsync(new { error = "not_found", message = ex.Message });
        }
        catch (ConflictException ex)
        {
            ctx.Response.StatusCode = StatusCodes.Status409Conflict;
            await ctx.Response.WriteAsJsonAsync(new { error = "conflict", message = ex.Message });
        }
        catch (UnauthorizedException ex)
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await ctx.Response.WriteAsJsonAsync(new { error = "unauthorized", message = ex.Message });
        }
        catch (EmailNotVerifiedException ex)
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            await ctx.Response.WriteAsJsonAsync(new { error = "email_not_verified", message = ex.Message });
        }
        catch (ReauthenticationFailedException ex)
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            await ctx.Response.WriteAsJsonAsync(new { error = "reauthentication_failed", message = ex.Message });
        }
        catch (FeatureDisabledException ex)
        {
            ctx.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await ctx.Response.WriteAsJsonAsync(new
            {
                error = "feature_disabled",
                feature = ex.FeatureCode,
                message = ex.Message
            });
        }
        catch (ProfileRequiredException ex)
        {
            ctx.Response.StatusCode = StatusCodes.Status409Conflict;
            await ctx.Response.WriteAsJsonAsync(new { error = "profile_required", message = ex.Message });
        }
        catch (AiPrivacyBlockedException ex)
        {
            ctx.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            await ctx.Response.WriteAsJsonAsync(new
            {
                error = "ai_privacy_blocked",
                code = ex.ReasonCode,
                message = ex.Message,
                safeResponse = ex.SafeResponse
            });
        }
        catch (QuestionValidationException ex)
        {
            ctx.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            await ctx.Response.WriteAsJsonAsync(new
            {
                error = ex.ErrorCode,
                message = ex.Message,
                suggestedQuestion = ex.SuggestedQuestion
            });
        }
        catch (QuestionRequiresSubscriptionException ex)
        {
            ctx.Response.StatusCode = StatusCodes.Status402PaymentRequired;
            await ctx.Response.WriteAsJsonAsync(new
            {
                error = "question_requires_subscription",
                message = ex.Message,
                reason = ex.Reason,
                suggestedQuestion = ex.SuggestedQuestion
            });
        }
        catch (QuestionWarningAcknowledgementRequiredException ex)
        {
            ctx.Response.StatusCode = StatusCodes.Status409Conflict;
            await ctx.Response.WriteAsJsonAsync(new
            {
                error = "question_warning_unacknowledged",
                message = ex.Message,
                status = ex.Status,
                reason = ex.Reason,
                suggestedQuestion = ex.SuggestedQuestion
            });
        }
        catch (QuotaExceededException ex)
        {
            ctx.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await ctx.Response.WriteAsJsonAsync(new { error = "quota_exceeded", message = ex.Message });
        }
        catch (SubscriptionRequiredException ex)
        {
            ctx.Response.StatusCode = StatusCodes.Status402PaymentRequired;
            await ctx.Response.WriteAsJsonAsync(new { error = "subscription_required", message = ex.Message });
        }
        catch (InvalidOperationException ex) when (IsAiConfigurationException(ex))
        {
            _logger.LogError(
                "AI provider is unavailable; errorType={ErrorType}; correlationId={CorrelationId}",
                ex.GetType().Name,
                ctx.TraceIdentifier);
            ctx.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await ctx.Response.WriteAsJsonAsync(new
            {
                error = "ai_not_configured",
                message = "AI-провайдер не настроен. Для локальной генерации укажи OpenAI:ApiKey или DeepSeek:ApiKey."
            });
        }
        catch (InvalidOperationException ex) when (IsExternalProcessorConfigurationException(ex))
        {
            _logger.LogError(
                "External processor is blocked; errorType={ErrorType}; correlationId={CorrelationId}",
                ex.GetType().Name,
                ctx.TraceIdentifier);
            ctx.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await ctx.Response.WriteAsJsonAsync(new
            {
                error = "external_processor_blocked",
                message = "Внешняя интеграция отключена до проверки оператором сервиса."
            });
        }
        catch (DomainException ex)
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            await ctx.Response.WriteAsJsonAsync(new { error = "bad_request", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "Unhandled exception; errorType={ErrorType}; correlationId={CorrelationId}",
                ex.GetType().Name,
                ctx.TraceIdentifier);
            ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await ctx.Response.WriteAsJsonAsync(new { error = "internal_error" });
        }
    }

    private static bool IsAiConfigurationException(InvalidOperationException ex)
    {
        return ex.Message.Contains(":ApiKey is not configured", StringComparison.Ordinal) ||
               ex.Message.Contains(":Model is not configured", StringComparison.Ordinal) ||
               ex.Message.Contains("AI:Provider", StringComparison.Ordinal) ||
               ex.Message.Contains("AI integration", StringComparison.Ordinal) ||
               ex.Message.Contains("AI endpoint", StringComparison.Ordinal) ||
               ex.Message.Contains("AI processor", StringComparison.Ordinal);
    }

    private static bool IsExternalProcessorConfigurationException(InvalidOperationException ex)
    {
        return ex.Message.Contains("Payment integration", StringComparison.Ordinal)
               || ex.Message.Contains("Payment product", StringComparison.Ordinal)
               || ex.Message.Contains("payment creation", StringComparison.OrdinalIgnoreCase)
               || ex.Message.Contains("credentials are not configured", StringComparison.Ordinal)
               || ex.Message.Contains("receiver is not configured", StringComparison.Ordinal)
               || ex.Message.Contains("Processor registry", StringComparison.Ordinal)
               || ex.Message.Contains("External processor", StringComparison.Ordinal)
               || ex.Message.Contains("external transfer", StringComparison.Ordinal);
    }
}
