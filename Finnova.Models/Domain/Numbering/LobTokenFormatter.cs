namespace Finnova.Models.Domain.Numbering;

/// <summary>Renders the {LOB} token: upper-case letters and digits of the LOB name, at most 10 characters.</summary>
public static class LobTokenFormatter
{
    public const int MaxLength = 10;

    public static string ToToken(string lobName)
    {
        var chars = (lobName ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).Take(MaxLength);
        return new string(chars.ToArray());
    }
}