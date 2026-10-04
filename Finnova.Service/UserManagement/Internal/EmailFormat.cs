namespace Finnova.Service.UserManagement.Internal;

/// <summary>
/// Email format rule shared by the validator and the property test (R4.4/4.5): &le;60 chars,
/// exactly one '@', at least one '.', and no leading/trailing '@' / '.' / special character.
/// </summary>
public static class EmailFormat
{
    public static bool IsValid(string? email)
    {
        if (string.IsNullOrEmpty(email)) return false;
        if (email.Length > 60) return false;
        if (email.Count(c => c == '@') != 1) return false;
        if (!email.Contains('.')) return false;

        var first = email[0];
        var last = email[^1];
        if (!char.IsLetterOrDigit(first) || !char.IsLetterOrDigit(last)) return false;

        return true;
    }
}
