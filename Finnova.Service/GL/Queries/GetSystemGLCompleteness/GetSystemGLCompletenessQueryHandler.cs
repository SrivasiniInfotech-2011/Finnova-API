using Finnova.Models.Contracts.GL;
using Finnova.Repository.Interfaces;
using Finnova.Service.GL.Internal;
using Finnova.Service.GL.Ports;
using MediatR;

namespace Finnova.Service.GL.Queries.GetSystemGLCompleteness;

public class GetSystemGLCompletenessQueryHandler
    : IRequestHandler<GetSystemGLCompletenessQuery, SystemGLCompletenessResponse>
{
    private readonly IGLSystemAccountMappingRepository _mappings;
    private readonly ICompanyQueryPort _company;

    public GetSystemGLCompletenessQueryHandler(
        IGLSystemAccountMappingRepository mappings, ICompanyQueryPort company)
    {
        _mappings = mappings;
        _company = company;
    }

    public Task<SystemGLCompletenessResponse> Handle(GetSystemGLCompletenessQuery request, CancellationToken ct)
        => Completeness.ComputeAsync(_mappings, _company, request.CompanyId, ct);
}
