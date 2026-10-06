using System.Linq;

namespace Finnova.Service.UserManagement.Abstractions;

/// <summary>
/// Development default for the GPS password policy: alphanumeric with special characters, min 8
/// chars, at least one letter, one digit, and one special character (R3 Password glossary). The
/// real GPS policy replaces this via DI when available.
/// </summary>
public class DefaultPasswordPolicy : IPasswordPolicy
{
    public bool IsCompliant(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 8) return false;
        var hasLetter = password.Any(char.IsLetter);
        var hasDigit = password.Any(char.IsDigit);
        var hasSpecial = password.Any(c => !char.IsLetterOrDigit(c));
        return hasLetter && hasDigit && hasSpecial;
    }
}
