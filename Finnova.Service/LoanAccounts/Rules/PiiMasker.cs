namespace Finnova.Service.LoanAccounts.Rules;

/// <summary>Masks sensitive identifiers. Same length as the input; reveals at most the last four characters (Property 12).</summary>
public static class PiiMasker
{
    public static string Mask(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Length <= 4) return new string('*', value.Length);
        return new string('*', value.Length - 4) + value[^4..];
    }
}