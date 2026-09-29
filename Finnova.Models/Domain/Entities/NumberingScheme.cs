using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

/// <summary>
/// A numbering scheme (FINNOVA-7): defines how numbers are generated for a document type + scope.
/// Holds both the configuration and the live sequence state (CurrentValue / PeriodKey), which is
/// advanced atomically by issuance. India-only platform: one English Name.
/// </summary>
public class NumberingScheme
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Code { get; set; } = string.Empty;          // max 30, unique (case-insensitive, trimmed) (R2.1)
    public string Name { get; set; } = string.Empty;          // max 100, English (R1.6)
    public string DocumentType { get; set; } = string.Empty;  // max 50, e.g. "LoanAccount" (R1.6)

    public string FormatTemplate { get; set; } = string.Empty; // max 100, e.g. "INV-{YYYY}{MM}-{SEQ:5}" (R1.12/1.13)
    public string? Prefix { get; set; }                        // max 20, substitutes {PREFIX}
    public string? Suffix { get; set; }                        // max 20, substitutes {SUFFIX}

    public long SeqStart { get; set; } = 1;                    // start value (>= 0) (R1.4/1.7)
    public int SeqIncrement { get; set; } = 1;                 // step (>= 1) (R1.4/1.7)
    public int SeqPadding { get; set; } = 1;                   // zero-pad width for {SEQ} (1..18) (R1.4/1.7)

    public NumberResetRule ResetRule { get; set; } = NumberResetRule.Never;

    public NumberScope Scope { get; set; } = NumberScope.Global;
    public Guid? ScopeId { get; set; }                         // required for Company/Branch, null for Global (R1.10/1.11)

    // Live sequence state, advanced only by issuance / reset (R5). CurrentValue 0 => none issued yet.
    public long CurrentValue { get; set; }
    public string PeriodKey { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;                 // default true (R1.2)

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
