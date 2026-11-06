using MediatR;
using Finnova.Models.Contracts.Applications;
using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.Lookups;
using Finnova.Models.Contracts.UserManagement;
using Finnova.Repository.Interfaces;
using Finnova.Service.Applications.Ports;
using Microsoft.Extensions.Configuration;

namespace Finnova.Service.Applications.Queries;

public sealed record GetActiveLinesOfBusinessQuery() : IRequest<List<LineOfBusinessRefResponse>>;
public sealed record SearchActiveCustomersQuery(string? Search, int Page = 1, int PageSize = 20) : IRequest<CustomerReferencePage>;
public sealed record GetActiveBranchesQuery(Guid? LineOfBusinessId = null) : IRequest<List<ApplicationReferenceOptionResponse>>;
public sealed record GetConfiguredLookupQuery(string Type) : IRequest<List<LookupDropdownItemResponse>>;
public sealed class GetActiveLinesOfBusinessQueryHandler : IRequestHandler<GetActiveLinesOfBusinessQuery, List<LineOfBusinessRefResponse>>
{ private readonly ILineOfBusinessReferencePort _p; public GetActiveLinesOfBusinessQueryHandler(ILineOfBusinessReferencePort p) => _p = p; public Task<List<LineOfBusinessRefResponse>> Handle(GetActiveLinesOfBusinessQuery r, CancellationToken c) => _p.GetActiveAsync(c); }
public sealed class SearchActiveCustomersQueryHandler : IRequestHandler<SearchActiveCustomersQuery, CustomerReferencePage>
{ private readonly ICustomerReferencePort _p; public SearchActiveCustomersQueryHandler(ICustomerReferencePort p) => _p = p; public async Task<CustomerReferencePage> Handle(SearchActiveCustomersQuery r, CancellationToken c) { var x = await _p.SearchActiveAsync(r.Search, r.Page, r.PageSize, c); return new(x.Items, x.Total, r.Page, r.PageSize, (int)Math.Ceiling(x.Total / (double)r.PageSize)); } }
public sealed class GetActiveBranchesQueryHandler : IRequestHandler<GetActiveBranchesQuery, List<ApplicationReferenceOptionResponse>>
{ private readonly IBranchReferencePort _p; public GetActiveBranchesQueryHandler(IBranchReferencePort p) => _p = p; public Task<List<ApplicationReferenceOptionResponse>> Handle(GetActiveBranchesQuery r, CancellationToken c) => _p.GetActiveAsync(r.LineOfBusinessId, c); }
public sealed class GetConfiguredLookupQueryHandler : IRequestHandler<GetConfiguredLookupQuery, List<LookupDropdownItemResponse>>
{ private readonly ILookupReferencePort _p; private readonly IConfiguration _c; public GetConfiguredLookupQueryHandler(ILookupReferencePort p, IConfiguration c) => (_p, _c) = (p, c); public Task<List<LookupDropdownItemResponse>> Handle(GetConfiguredLookupQuery r, CancellationToken c) => _p.GetActiveAsync(_c["ApplicationProcessing:LookupModule"] ?? throw new Models.Domain.Exceptions.ApplicationConfigurationException("Lookup module is required."), r.Type, c); }
