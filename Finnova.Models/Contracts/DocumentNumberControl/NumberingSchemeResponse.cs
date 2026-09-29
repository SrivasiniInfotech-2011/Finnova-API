using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Contracts.DocumentNumberControl;

/// <summary>Admin-facing scheme response, including the live sequence state (R4.9).</summary>
public record NumberingSchemeResponse(
    Guid Id,
    string Code,
    string Name,
    string DocumentType,
    string FormatTemplate,
    string? Prefix,
    string? Suffix,
    long SeqStart,
    int SeqIncrement,
    int SeqPadding,
    NumberResetRule ResetRule,
    NumberScope Scope,
    Guid? ScopeId,
    long CurrentValue,
    string PeriodKey,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);
