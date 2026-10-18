namespace Finnova.Service.GL.Internal;

/// <summary>Pure GL-code rules (R1.5/R1.6). No I/O — property-testable.</summary>
public static class GLCodeRules
{
    /// <summary>True iff the string is exactly nine ASCII digits (0-9); no whitespace. (R1.5)</summary>
    public static bool IsNineNumericDigits(string? code)
    {
        if (code is null || code.Length != 9) return false;
        foreach (var ch in code)
            if (ch < '0' || ch > '9') return false;
        return true;
    }

    /// <summary>True iff childCode begins with parentCode as a leading substring. (R1.6)</summary>
    public static bool BeginsWithParentPrefix(string? childCode, string? parentCode)
    {
        if (childCode is null || parentCode is null) return false;
        return childCode.StartsWith(parentCode, StringComparison.Ordinal);
    }
}
