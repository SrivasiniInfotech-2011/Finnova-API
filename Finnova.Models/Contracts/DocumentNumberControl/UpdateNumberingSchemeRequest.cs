using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Contracts.DocumentNumberControl;

/// <summary>
/// Request to update a numbering scheme's editable fields. Code, DocumentType, Scope, and ScopeId
/// are immutable after creation (R2.6, R3.2); the live sequence state is never edited (R3.3).
/// </summary>
public record UpdateNumberingSchemeRequest(
    string Name,
    string FormatTemplate,
    string? Prefix,
    string? Suffix,
    int SeqIncrement,
    int SeqPadding,
    NumberResetRule ResetRule,
    bool IsActive);
