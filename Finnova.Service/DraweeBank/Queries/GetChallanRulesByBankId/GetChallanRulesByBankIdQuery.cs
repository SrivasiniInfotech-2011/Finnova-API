using MediatR;
using Finnova.Models.Contracts.DraweeBanks;

namespace Finnova.Service.DraweeBank.Queries.GetChallanRulesByBankId;

/// <summary>Reads the challan rules configured under one drawee bank (R6). Rules are decoupled
/// from the bank aggregate and read only through this query / its endpoint.</summary>
public record GetChallanRulesByBankIdQuery(
    Guid DraweeBankId
) : IRequest<List<ChallanRuleResponse>>;
