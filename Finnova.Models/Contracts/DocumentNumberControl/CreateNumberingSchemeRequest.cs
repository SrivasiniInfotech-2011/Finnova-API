using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Contracts.DocumentNumberControl;

/// <summary>
/// Request to create a numbering scheme. Sequence state (CurrentValue/PeriodKey) is never accepted
/// from the client; nullable fields are defaulted by the service (R1.2-R1.4).
/// </summary>
public record CreateNumberingSchemeRequest(
    string Code,
    string Name,
    string DocumentType,
    string FormatTemplate,
    string? Prefix,
    string? Suffix,
    long? SeqStart,
    int? SeqIncrement,
    int? SeqPadding,
    NumberResetRule ResetRule,
    NumberScope? Scope,
    Guid? ScopeId,
    bool? IsActive);
