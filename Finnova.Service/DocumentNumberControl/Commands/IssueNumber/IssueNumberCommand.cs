using Finnova.Models.Contracts.DocumentNumberControl;
using Finnova.Models.Domain.Enums;
using MediatR;

namespace Finnova.Service.DocumentNumberControl.Commands.IssueNumber;

/// <summary>Issues the next number for a document type + scope (R5). Not audited (R6.4).</summary>
public record IssueNumberCommand(string DocumentType, NumberScope Scope, Guid? ScopeId, string ActingAdmin)
    : IRequest<IssuedNumberResponse>;
