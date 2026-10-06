namespace Finnova.Models.Domain.Exceptions;

/// <summary>Thrown when a user/group/functional-group record is not found. Maps to ERR-USR-404.</summary>
public class UserNotFoundException : Exception
{
    public const string ErrorCode = "ERR-USR-404";
    public UserNotFoundException(string key) : base($"User record '{key}' was not found.") { }
}
