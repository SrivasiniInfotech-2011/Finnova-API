using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Contracts.DocumentNumberControl;

/// <summary>Request to issue the next number for a document type + scope (R5).</summary>
public record IssueNumberRequest(string DocumentType, NumberScope Scope, Guid? ScopeId);
