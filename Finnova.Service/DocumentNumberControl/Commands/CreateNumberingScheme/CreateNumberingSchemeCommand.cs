using Finnova.Models.Contracts.DocumentNumberControl;
using Finnova.Models.Domain.Enums;
using MediatR;

namespace Finnova.Service.DocumentNumberControl.Commands.CreateNumberingScheme;

/// <summary>
/// Creates a numbering scheme. ActingAdmin is resolved from the JWT by the controller and recorded
/// on the create audit entry (R1, R6.1).
/// </summary>
public record CreateNumberingSchemeCommand(
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
    bool? IsActive,
    string ActingAdmin) : IRequest<NumberingSchemeResponse>;
