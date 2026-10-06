namespace Finnova.Models.Domain.Exceptions;

/// <summary>Thrown when a generated code collides and cannot be made unique (R2.5). Maps to ERR-USR-409.</summary>
public class UserDuplicateCodeException : Exception
{
    public const string ErrorCode = "ERR-USR-409";
    public UserDuplicateCodeException() : base("Generated code collided and could not be made unique.") { }
}
