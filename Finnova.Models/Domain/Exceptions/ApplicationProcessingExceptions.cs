namespace Finnova.Models.Domain.Exceptions;

public abstract class ApplicationProcessingException : Exception
{
    protected ApplicationProcessingException(string message) : base(message) { }
    public abstract string ErrorCode { get; }
}

public sealed class ApplicationNotFoundException : ApplicationProcessingException
{
    public ApplicationNotFoundException(Guid id) : base($"Application '{id}' was not found.") { }
    public override string ErrorCode => "ERR-APP-404";
}

public sealed class ApplicationAlreadyProcessedException : ApplicationProcessingException
{
    public ApplicationAlreadyProcessedException() : base("A processed application cannot be modified or processed again.") { }
    public override string ErrorCode => "ERR-APP-409";
}

public sealed class ApplicationReferenceInvalidException : ApplicationProcessingException
{
    public ApplicationReferenceInvalidException(string field) : base($"{field} does not refer to an active reference item.") { }
    public override string ErrorCode => "ERR-APP-400";
}

public sealed class ApplicationConflictException : ApplicationProcessingException
{
    public ApplicationConflictException(string message = "The application was changed by another request.") : base(message) { }
    public override string ErrorCode => "ERR-APP-409";
}

public sealed class ApplicationConfigurationException : ApplicationProcessingException
{
    public ApplicationConfigurationException(string message) : base(message) { }
    public override string ErrorCode => "ERR-APP-503";
}
