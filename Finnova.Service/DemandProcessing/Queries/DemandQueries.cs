using System.Security.Claims;
using MediatR;
using Finnova.Models.Contracts.DemandProcessing;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.DemandProcessing.Mappers;
using Finnova.Service.DemandProcessing.Ports;
using Microsoft.Extensions.Options;
using Finnova.Service.DemandProcessing.Configuration;

namespace Finnova.Service.DemandProcessing.Queries;

public sealed record GetDemandRunQuery(Guid RunId, ClaimsPrincipal Principal) : IRequest<DemandRunResponse>;
public sealed record GetDemandHistoryQuery(DemandHistoryQuery Query, ClaimsPrincipal Principal) : IRequest<PaginatedResponse<DemandRunResponse>>;
public sealed record GetDemandRowsQuery(DemandRowsQuery Query, ClaimsPrincipal Principal) : IRequest<PaginatedResponse<DemandRowResponse>>;
public sealed record GetDemandBatchesQuery(DemandBatchesQuery Query, ClaimsPrincipal Principal) : IRequest<PaginatedResponse<DemandBatchResponse>>;

public sealed class DemandQueryHandlers(IDemandRepository repository, IAuthorizationScopeResolver scopes, IOptions<DemandProcessingOptions> options) : IRequestHandler<GetDemandRunQuery, DemandRunResponse>, IRequestHandler<GetDemandHistoryQuery, PaginatedResponse<DemandRunResponse>>, IRequestHandler<GetDemandRowsQuery, PaginatedResponse<DemandRowResponse>>, IRequestHandler<GetDemandBatchesQuery, PaginatedResponse<DemandBatchResponse>>
{
    public async Task<DemandRunResponse> Handle(GetDemandRunQuery request, CancellationToken ct) { var allowed = await scopes.ResolveAsync(request.Principal, ct); var run = await repository.GetRunAsync(request.RunId, ct) ?? throw new DemandNotFoundException(request.RunId); if (!run.BranchScopes.Any(x => allowed.BranchIds.Contains(x.BranchId))) throw new DemandNotFoundException(request.RunId); var revision = run.Revisions.Single(x => x.RevisionNumber == run.CurrentRevisionNumber); return new(run.Id, revision.RevisionNumber, run.LineOfBusinessId, run.BranchScopes.OrderBy(x => x.SortOrder).Select(x => x.BranchId).ToArray(), run.Frequency, run.PeriodYear, run.PeriodMonth, run.ProcessingCutoffReference, run.Status, run.BillingStatus, run.BillingSnapshotId, null, run.DemandSnapshotId, run.RowCount, run.BatchCount, run.CurrentDueTotal, run.ArrearsTotal, run.OdiTotal, run.OtherChargesTotal, run.TotalCharges, run.TotalDemand, Array.Empty<DemandFailureResponse>(), run.RequestedAtUtc, run.CompletedAtUtc); }
    public async Task<PaginatedResponse<DemandRunResponse>> Handle(GetDemandHistoryQuery request, CancellationToken ct) { var allowed = await scopes.ResolveAsync(request.Principal, ct); var pageSize = Math.Clamp(request.Query.PageSize, 1, options.Value.MaxPageSize); var query = request.Query with { Page = Math.Max(1, request.Query.Page), PageSize = pageSize }; var result = await repository.SearchRunsAsync(query, allowed.BranchIds, ct); var data = result.Items.Select(x => new DemandRunResponse(x.Id, x.CurrentRevisionNumber, x.LineOfBusinessId, x.BranchScopes.OrderBy(s => s.SortOrder).Select(s => s.BranchId).ToArray(), x.Frequency, x.PeriodYear, x.PeriodMonth, x.ProcessingCutoffReference, x.Status, x.BillingStatus, x.BillingSnapshotId, null, x.DemandSnapshotId, x.RowCount, x.BatchCount, x.CurrentDueTotal, x.ArrearsTotal, x.OdiTotal, x.OtherChargesTotal, x.TotalCharges, x.TotalDemand, Array.Empty<DemandFailureResponse>(), x.RequestedAtUtc, x.CompletedAtUtc)).ToArray(); return new(data, result.Total, query.Page, query.PageSize, (int)Math.Ceiling(result.Total / (double)query.PageSize)); }
    public async Task<PaginatedResponse<DemandRowResponse>> Handle(GetDemandRowsQuery request, CancellationToken ct) { var allowed = await scopes.ResolveAsync(request.Principal, ct); var pageSize = Math.Clamp(request.Query.PageSize, 1, options.Value.MaxPageSize); var result = await repository.SearchRowsAsync(request.Query with { Page = Math.Max(1, request.Query.Page), PageSize = pageSize }, allowed.BranchIds, ct); var data = result.Items.Select(x => x.ToResponse()).ToArray(); return new(data, result.Total, request.Query.Page, pageSize, (int)Math.Ceiling(result.Total / (double)pageSize)); }
    public async Task<PaginatedResponse<DemandBatchResponse>> Handle(GetDemandBatchesQuery request, CancellationToken ct) { var allowed = await scopes.ResolveAsync(request.Principal, ct); var pageSize = Math.Clamp(request.Query.PageSize, 1, options.Value.MaxPageSize); var result = await repository.SearchBatchesAsync(request.Query with { Page = Math.Max(1, request.Query.Page), PageSize = pageSize }, allowed.BranchIds, ct); var data = result.Items.Select(x => x.ToResponse()).ToArray(); return new(data, result.Total, request.Query.Page, pageSize, (int)Math.Ceiling(result.Total / (double)pageSize)); }
}
