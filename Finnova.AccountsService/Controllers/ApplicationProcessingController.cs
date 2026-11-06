using Finnova.Models.Contracts.Applications;
using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.Lookups;
using Finnova.Models.Contracts.UserManagement;
using Finnova.Service.Applications.Commands;
using Finnova.Service.Applications.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Finnova.AccountsService.Controllers;

[ApiController, Route("api/application-processing"), Authorize(Policy = "ApplicationProcessor")]
public sealed class ApplicationProcessingController : ControllerBase
{
    private readonly IMediator _mediator;
    public ApplicationProcessingController(IMediator mediator) => _mediator = mediator;
    private string Actor => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? User.Identity?.Name ?? "authenticated-user";
    [HttpPost]
    public async Task<ActionResult<ApplicationResponse>> Create(CreateApplicationRequest r, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateApplicationCommand(r.LineOfBusinessId, r.CustomerId, r.BranchId, r.ProductCode, r.SchemeCode, r.FinanceAmount, r.IrrPercent, r.Tenure, r.RepaymentFrequency, r.DisbursementDate, r.ProcessingCharges, Actor), ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }
    [HttpPut("{id:guid}")] public async Task<ActionResult<ApplicationResponse>> Update(Guid id, UpdateApplicationRequest r, CancellationToken ct) => Ok(await _mediator.Send(new UpdateApplicationCommand(id, r.LineOfBusinessId, r.CustomerId, r.BranchId, r.ProductCode, r.SchemeCode, r.FinanceAmount, r.IrrPercent, r.Tenure, r.RepaymentFrequency, r.DisbursementDate, r.ProcessingCharges, Actor), ct));
    [HttpGet] public async Task<ActionResult<PaginatedResponse<ApplicationResponse>>> Search([FromQuery] ApplicationSearchRequest r, CancellationToken ct) => Ok(await _mediator.Send(new GetApplicationsQuery(r.Search, r.LineOfBusinessId, r.CustomerId, r.ProductCode, r.BranchId, r.SchemeCode, r.Page, r.PageSize), ct));
    [HttpGet("{id:guid}")] public async Task<ActionResult<ApplicationResponse>> GetById(Guid id, CancellationToken ct) => Ok(await _mediator.Send(new GetApplicationByIdQuery(id), ct));
    [HttpGet("{id:guid}/schedule")] public async Task<ActionResult<ApplicationScheduleResponse>> Schedule(Guid id, CancellationToken ct) => Ok(await _mediator.Send(new GetApplicationScheduleQuery(id), ct));
    [HttpPost("{id:guid}/schedule/preview")] public async Task<ActionResult<ApplicationScheduleResponse>> Preview(Guid id, CancellationToken ct) => Ok(await _mediator.Send(new PreviewApplicationScheduleQuery(id), ct));
    [HttpPost("{id:guid}/process")] public async Task<ActionResult<ProcessApplicationResponse>> Process(Guid id, CancellationToken ct) => Ok(await _mediator.Send(new ProcessApplicationCommand(id, Actor), ct));
    [HttpGet("references/lines-of-business")] public async Task<ActionResult<List<LineOfBusinessRefResponse>>> Lobs(CancellationToken ct) => Ok(await _mediator.Send(new GetActiveLinesOfBusinessQuery(), ct));
    [HttpGet("references/customers")] public async Task<ActionResult<CustomerReferencePage>> Customers([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) => Ok(await _mediator.Send(new SearchActiveCustomersQuery(search, page, pageSize), ct));
    [HttpGet("references/branches")] public async Task<ActionResult<List<ApplicationReferenceOptionResponse>>> Branches([FromQuery] Guid? lineOfBusinessId, CancellationToken ct) => Ok(await _mediator.Send(new GetActiveBranchesQuery(lineOfBusinessId), ct));
    [HttpGet("references/lookups/{type}")] public async Task<ActionResult<List<LookupDropdownItemResponse>>> Lookup(string type, CancellationToken ct) => Ok(await _mediator.Send(new GetConfiguredLookupQuery(type), ct));
}
