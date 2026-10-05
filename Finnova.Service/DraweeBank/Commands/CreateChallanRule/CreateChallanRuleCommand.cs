using MediatR;
using Finnova.Models.Contracts.DraweeBanks;

namespace Finnova.Service.DraweeBank.Commands.CreateChallanRule;

public record CreateChallanRuleCommand(
    Guid DraweeBankId,
    string RuleCode,
    string FormatPattern,
    string ValidationExpression,
    string RoutingTarget,
    bool? IsActive,
    string ActingAdmin
) : IRequest<ChallanRuleResponse>;
