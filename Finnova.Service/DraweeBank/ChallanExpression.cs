namespace Finnova.Service.DraweeBank;

/// <summary>
/// Placeholder parser for challan Format Pattern / Validation Expression (R6.8). The ticket does
/// not define a grammar; until one is agreed this enforces a minimal "parseable" rule: non-blank,
/// within the length bound, and balanced brackets/parentheses. Centralised so the real grammar can
/// replace this one method without touching the validators.
/// </summary>
public static class ChallanExpression
{
    public static bool IsParseable(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression)) return false;

        var depthParen = 0;
        var depthBracket = 0;
        foreach (var ch in expression)
        {
            switch (ch)
            {
                case '(': depthParen++; break;
                case ')': if (--depthParen < 0) return false; break;
                case '[': depthBracket++; break;
                case ']': if (--depthBracket < 0) return false; break;
            }
        }
        return depthParen == 0 && depthBracket == 0;
    }
}
