using Finnova.Models.Contracts.DocumentNumberControl;
using Finnova.Models.Domain.Enums;
using MediatR;

namespace Finnova.Service.DocumentNumberControl.Commands.UpdateNumberingScheme;

/// <summary>
/// Updates a scheme's editable fields (R3). Code, DocumentType, Scope, and ScopeId are immutable
/// and not part of this command; the live sequence state is never edited.
/// </summary>
public record UpdateNumberingSchemeCommand(
    Guid Id,
    string Name,
    string FormatTemplate,
    string? Prefix,
    string? Suffix,
    int SeqIncrement,
    int SeqPadding,
    NumberResetRule ResetRule,
    bool IsActive,
    string ActingAdmin) : IRequest<NumberingSchemeResponse>;
