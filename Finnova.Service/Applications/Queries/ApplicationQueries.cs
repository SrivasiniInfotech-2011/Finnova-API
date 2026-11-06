using MediatR;
using Finnova.Models.Contracts.Applications;
using Finnova.Models.Contracts.Common;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;

namespace Finnova.Service.Applications.Queries;

public sealed record GetApplicationsQuery(string? Search, Guid? LineOfBusinessId, Guid? CustomerId, string? ProductCode,
    Guid? BranchId, string? SchemeCode, int Page = 1, int PageSize = 20) : IRequest<PaginatedResponse<ApplicationResponse>>;
public sealed record GetApplicationByIdQuery(Guid Id) : IRequest<ApplicationResponse>;
public sealed record GetApplicationScheduleQuery(Guid Id) : IRequest<ApplicationScheduleResponse>;

public sealed class GetApplicationsQueryHandler : IRequestHandler<GetApplicationsQuery, PaginatedResponse<ApplicationResponse>>
{
    private readonly IApplicationRepository _repo;
    public GetApplicationsQueryHandler(IApplicationRepository repo) => _repo = repo;
    public async Task<PaginatedResponse<ApplicationResponse>> Handle(GetApplicationsQuery r, CancellationToken ct)
    {
        var result = await _repo.SearchAsync(new ApplicationFilters(r.Search, r.LineOfBusinessId, r.CustomerId, r.ProductCode, r.BranchId, r.SchemeCode, r.Page, r.PageSize), ct);
        return new PaginatedResponse<ApplicationResponse>(result.Items.Select(x => x.ToResponse()).ToList(), result.Total, r.Page, r.PageSize, (int)Math.Ceiling(result.Total / (double)r.PageSize));
    }
}
public sealed class GetApplicationByIdQueryHandler : IRequestHandler<GetApplicationByIdQuery, ApplicationResponse>
{
    private readonly IApplicationRepository _repo;
    public GetApplicationByIdQueryHandler(IApplicationRepository repo) => _repo = repo;
    public async Task<ApplicationResponse> Handle(GetApplicationByIdQuery r, CancellationToken ct) =>
        (await _repo.GetByIdAsync(r.Id, ct))?.ToResponse() ?? throw new ApplicationNotFoundException(r.Id);
}
public sealed class GetApplicationScheduleQueryHandler : IRequestHandler<GetApplicationScheduleQuery, ApplicationScheduleResponse>
{
    private readonly IApplicationRepository _apps; private readonly IApplicationScheduleRepository _schedules; private readonly IMediator _mediator;
    public GetApplicationScheduleQueryHandler(IApplicationRepository apps, IApplicationScheduleRepository schedules, IMediator mediator)
        => (_apps, _schedules, _mediator) = (apps, schedules, mediator);
    public async Task<ApplicationScheduleResponse> Handle(GetApplicationScheduleQuery r, CancellationToken ct)
    {
        var a = await _apps.GetByIdAsync(r.Id, ct) ?? throw new ApplicationNotFoundException(r.Id);
        if (a.Status != ApplicationStatus.Processed) return await _mediator.Send(new Commands.PreviewApplicationScheduleQuery(r.Id), ct);
        var s = await _schedules.GetByApplicationIdAsync(r.Id, ct) ?? throw new ApplicationConflictException("Final schedule is missing.");
        return s.ToResponse();
    }
}
