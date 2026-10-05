using MediatR;
using Finnova.Models.Contracts.DraweeBanks;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;

namespace Finnova.Service.DraweeBank.Queries.GetChallanRulesByBankId;

public class GetChallanRulesByBankIdQueryHandler
    : IRequestHandler<GetChallanRulesByBankIdQuery, List<ChallanRuleResponse>>
{
    private readonly IDraweeBankRepository _repository;

    public GetChallanRulesByBankIdQueryHandler(IDraweeBankRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<ChallanRuleResponse>> Handle(
        GetChallanRulesByBankIdQuery request, CancellationToken ct)
    {
        // Standalone read — never loaded with the bank. Missing bank id yields an empty list.
        var rules = await _repository.GetChallanRulesByBankIdAsync(request.DraweeBankId, ct);
        return rules.Select(r => r.ToResponse()).ToList();
    }
}
