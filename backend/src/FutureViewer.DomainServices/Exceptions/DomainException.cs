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

public sealed class SubscriptionAlreadyActiveException : DomainException
{
    public SubscriptionAlreadyActiveException(string message) : base(message) { }
}

public sealed class EmailNotVerifiedException : DomainException
{
    public EmailNotVerifiedException(string message) : base(message) { }
}
