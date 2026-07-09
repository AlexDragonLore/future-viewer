namespace FutureViewer.DomainServices.Exceptions;

public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}

public sealed class NotFoundException : DomainException
{
    public NotFoundException(string message) : base(message) { }
}

public sealed class ConflictException : DomainException
{
    public ConflictException(string message) : base(message) { }
}

public sealed class UnauthorizedException : DomainException
{
    public UnauthorizedException(string message) : base(message) { }
}

public sealed class QuotaExceededException : DomainException
{
    public QuotaExceededException(string message) : base(message) { }
}

public sealed class SubscriptionRequiredException : DomainException
{
    public SubscriptionRequiredException(string message) : base(message) { }
}

public sealed class QuestionRequiresSubscriptionException : DomainException
{
    public QuestionRequiresSubscriptionException(
        string message,
        string reason,
        string? suggestedQuestion = null) : base(message)
    {
        Reason = reason;
        SuggestedQuestion = suggestedQuestion;
    }

    public string Reason { get; }
    public string? SuggestedQuestion { get; }
}

public sealed class QuestionWarningAcknowledgementRequiredException : DomainException
{
    public QuestionWarningAcknowledgementRequiredException(
        string message,
        string reason,
        string status,
        string? suggestedQuestion = null) : base(message)
    {
        Reason = reason;
        Status = status;
        SuggestedQuestion = suggestedQuestion;
    }

    public string Reason { get; }
    public string Status { get; }
    public string? SuggestedQuestion { get; }
}

public sealed class EmailNotVerifiedException : DomainException
{
    public EmailNotVerifiedException(string message) : base(message) { }
}

public sealed class ProfileRequiredException : DomainException
{
    public ProfileRequiredException(string message) : base(message) { }
}

public sealed class QuestionValidationException : DomainException
{
    public QuestionValidationException(string errorCode, string message, string? suggestedQuestion = null)
        : base(message)
    {
        ErrorCode = errorCode;
        SuggestedQuestion = suggestedQuestion;
    }

    public string ErrorCode { get; }
    public string? SuggestedQuestion { get; }
}
