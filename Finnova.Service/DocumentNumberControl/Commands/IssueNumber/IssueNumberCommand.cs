using Finnova.Models.Contracts.DocumentNumberControl;
using Finnova.Models.Domain.Enums;
using MediatR;

namespace Finnova.Service.DocumentNumberControl.Commands.IssueNumber;

/// <summary>
/// Issues the next number (R5). Not audited (R6.4). With <paramref name="UseFallback"/> the scheme is resolved
/// server-side by the pure SchemeResolver from the LOB and Branch (application-account-creation R16.8); the
/// admin endpoint keeps the exact-scope behavior.
/// </summary>
public record IssueNumberCommand(
    string DocumentType, NumberScope Scope, Guid? ScopeId, string ActingAdmin,
    Guid? LineOfBusinessId = null, Guid? BranchId = null, bool UseFallback = false)
    : IRequest<IssuedNumberResponse>;