namespace Finnova.Models.Domain.Exceptions;

/// <summary>Thrown when a non-active user is tagged to a group (R5.8). Maps to ERR-USR-409.</summary>
public class UserInactiveMemberException : Exception
{
    public const string ErrorCode = "ERR-USR-409";
    public UserInactiveMemberException() : base("Only active users can be tagged to a group.") { }
}
