using System.Text;

namespace Finnova.Service.UserManagement.Helpers;

/// <summary>
/// Generates a 4-6 char, uppercase, alphanumeric code whose first char is a letter, derived from a
/// source string (user name OR role-center name) plus a number, with no spaces/specials
/// (R2.1-2.3). On collision the numeric suffix is varied and retried against <paramref name="isTaken"/>
/// (R2.5). After a bounded number of attempts throws so the handler can surface ERR-USR-409.
/// Pure except for the injected collision predicate — property-tested (P1).
/// </summary>
public static class UserCodeGenerator
{
    private const int MinLen = 4;
    private const int MaxLen = 6;
    private const int MaxAttempts = 10_000;

    public static string Generate(string source, Func<string, bool> isTaken)
    {
        var letters = new string((source ?? string.Empty)
            .ToUpperInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());

        // Ensure the first character is a letter (R2.1). Fall back to 'U' (User) when none present.
        var firstLetter = letters.FirstOrDefault(char.IsLetter);
        if (firstLetter == default) firstLetter = 'U';

        // Alpha stem (letters only, drives readability), bounded to leave room for the number.
        var alpha = new string(letters.Where(char.IsLetter).ToArray());
        if (alpha.Length == 0) alpha = firstLetter.ToString();
        var stem = (firstLetter + (alpha.Length > 1 ? alpha[1..] : string.Empty));

        for (var n = 1; n <= MaxAttempts; n++)
        {
            var numeric = n.ToString();
            // Keep total length within 4-6: trim the stem so stem+number fits, min 1 letter.
            var maxStem = Math.Max(1, MaxLen - numeric.Length);
            var usableStem = stem.Length > maxStem ? stem[..maxStem] : stem;

            var candidate = usableStem + numeric;
            if (candidate.Length < MinLen)
                candidate = usableStem + numeric.PadLeft(MinLen - usableStem.Length, '0');
            if (candidate.Length > MaxLen)
                candidate = candidate[..MaxLen];

            candidate = candidate.ToUpperInvariant();
            if (candidate.Length >= MinLen && candidate.Length <= MaxLen
                && char.IsLetter(candidate[0]) && candidate.All(char.IsLetterOrDigit)
                && !isTaken(candidate))
                return candidate;
        }

        throw new InvalidOperationException("Unable to generate a unique code within the attempt budget.");
    }
}
