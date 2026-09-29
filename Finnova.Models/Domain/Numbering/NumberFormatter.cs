using System.Globalization;
using System.Text.RegularExpressions;

namespace Finnova.Models.Domain.Numbering;

/// <summary>
/// Parses, validates, and renders numbering-scheme format templates (FINNOVA-7 R1.12/1.13, R5.5).
///
/// Supported tokens (case-sensitive): {PREFIX} {SUFFIX} {SEQ} {SEQ:n} {YYYY} {YY} {MM} {DD}.
/// Any other {...} token is invalid. A template MUST contain exactly one sequence token
/// ({SEQ} or {SEQ:n}). Literal text outside braces passes through unchanged. Pure; lives in Models
/// so both the repository (issuance overflow guard) and the service (validation + rendering) use it.
/// </summary>
public static partial class NumberFormatter
{
    // Matches a brace-delimited token, capturing its inner content, e.g. "SEQ", "SEQ:5", "YYYY".
    [GeneratedRegex(@"\{([^{}]*)\}", RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();

    /// <summary>Largest {SEQ:n} width allowed (matches the SeqPadding upper bound); 18 fits in long.</summary>
    public const int MaxSeqWidth = 18;

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>
    /// Validates a template. Returns an empty list when valid, otherwise one message per problem
    /// (unknown token, malformed SEQ width, missing/duplicate sequence token).
    /// </summary>
    public static IReadOnlyList<string> Validate(string template)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(template))
        {
            errors.Add("Format template is required.");
            return errors;
        }

        var seqTokenCount = 0;
        foreach (Match m in TokenRegex().Matches(template))
        {
            var inner = m.Groups[1].Value;
            switch (inner)
            {
                case "PREFIX":
                case "SUFFIX":
                case "YYYY":
                case "YY":
                case "MM":
                case "DD":
                    break;
                case "SEQ":
                    seqTokenCount++;
                    break;
                default:
                    if (inner.StartsWith("SEQ:", StringComparison.Ordinal))
                    {
                        var widthText = inner["SEQ:".Length..];
                        if (!int.TryParse(widthText, NumberStyles.None, Inv, out var width)
                            || width < 1 || width > MaxSeqWidth)
                        {
                            errors.Add($"Invalid sequence token '{{{inner}}}': width must be an integer between 1 and {MaxSeqWidth}.");
                        }
                        seqTokenCount++;
                    }
                    else
                    {
                        errors.Add($"Unknown token '{{{inner}}}' in format template.");
                    }
                    break;
            }
        }

        if (seqTokenCount == 0)
            errors.Add("Format template must include a sequence token ({SEQ} or {SEQ:n}).");
        else if (seqTokenCount > 1)
            errors.Add("Format template must include exactly one sequence token.");

        return errors;
    }

    /// <summary>
    /// Renders the number. {SEQ} pads to <paramref name="defaultPadding"/>; {SEQ:n} pads to n.
    /// {PREFIX}/{SUFFIX} substitute the literals (empty when null). Date tokens use the UTC instant.
    /// Padding is a MINIMUM width — a value wider than the pad is not truncated.
    /// </summary>
    public static string Format(
        string template, long sequenceValue, int defaultPadding,
        string? prefix, string? suffix, DateTime utcNow)
    {
        return TokenRegex().Replace(template, m =>
        {
            var inner = m.Groups[1].Value;
            return inner switch
            {
                "PREFIX" => prefix ?? string.Empty,
                "SUFFIX" => suffix ?? string.Empty,
                "YYYY" => utcNow.ToString("yyyy", Inv),
                "YY" => utcNow.ToString("yy", Inv),
                "MM" => utcNow.ToString("MM", Inv),
                "DD" => utcNow.ToString("dd", Inv),
                "SEQ" => PadSeq(sequenceValue, defaultPadding),
                _ when inner.StartsWith("SEQ:", StringComparison.Ordinal)
                    => PadSeq(sequenceValue, ParseWidth(inner["SEQ:".Length..], defaultPadding)),
                _ => m.Value // unknown tokens left verbatim; Validate() blocks them at write time
            };
        });
    }

    /// <summary>
    /// The largest sequence value representable at a given padding width, e.g. width 5 -> 99999.
    /// Used to detect exhaustion (R5.9). Widths at/above 19 saturate to long.MaxValue.
    /// </summary>
    public static long MaxValueForPadding(int padding)
    {
        if (padding >= 19) return long.MaxValue;
        long max = 1;
        for (var i = 0; i < padding; i++) max *= 10;
        return max - 1;
    }

    private static string PadSeq(long value, int width) =>
        value.ToString(Inv).PadLeft(width, '0');

    private static int ParseWidth(string text, int fallback) =>
        int.TryParse(text, NumberStyles.None, Inv, out var w) ? w : fallback;
}
