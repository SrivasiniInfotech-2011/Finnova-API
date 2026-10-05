using MediatR;
using Finnova.Models.Contracts.DraweeBanks;

namespace Finnova.Service.DraweeBank.Commands.UpdateChallanRule;

/// <summary>Updates a challan rule's mutable fields (RuleCode immutable). ActingAdmin from JWT.</summary>
public record UpdateChallanRuleCommand(
    Guid DraweeBankId,
    Guid RuleId,
    string FormatPattern,
    string ValidationExpression,
    string RoutingTarget,
    bool IsActive,
    string ActingAdmin
) : IRequest<ChallanRuleResponse>;
