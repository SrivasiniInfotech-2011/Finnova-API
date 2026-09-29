namespace Finnova.Models.Domain.Enums;

/// <summary>The period at which a scheme's sequence resets to its start value (FINNOVA-7 R1.8, R5.3).</summary>
public enum NumberResetRule
{
    Never = 0,
    Yearly = 1,
    Monthly = 2,
    Daily = 3
}
