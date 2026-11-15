using System.Security.Claims;
using Finnova.Models.Contracts.DemandProcessing;
using Finnova.Models.Domain.Enums;
using Finnova.Service.DemandProcessing.Commands;
using Finnova.Service.DemandProcessing.Ports;
using Finnova.Service.DemandProcessing.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finnova.AccountsService.Controllers;

[ApiController, Route("api/demand-processing")]
public sealed class DemandProcessingController(IMediator mediator, IDemandPdfExportService exports) : ControllerBase
{
    private string Actor => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? User.Identity?.Name ?? "authenticated-user";
    private string Correlation => Request.Headers.TryGetValue("X-Correlation-Id", out var value) && !string.IsNullOrWhiteSpace(value) ? value.ToString() : HttpContext.TraceIdentifier;
    private DemandSource Source => User.IsInRole("Automizer") ? DemandSource.Automizer : DemandSource.User;

    [HttpPost("runs"), Authorize(Policy = "DemandRun")]
    public async Task<ActionResult<DemandRunAcceptedResponse>> Start(StartDemandRunRequest request, CancellationToken ct) => Accepted(await mediator.Send(new StartDemandRunCommand(request, User, Actor, Source, Correlation), ct));
    [HttpGet("runs/{runId:guid}"), Authorize(Policy = "DemandOutputRead")]
    public async Task<ActionResult<DemandRunResponse>> GetRun(Guid runId, CancellationToken ct) => Ok(await mediator.Send(new GetDemandRunQuery(runId, User), ct));
    [HttpGet("runs"), Authorize(Policy = "DemandOutputRead")]
    public async Task<ActionResult<PaginatedResponse<DemandRunResponse>>> History([FromQuery] DemandHistoryQuery query, CancellationToken ct) => Ok(await mediator.Send(new GetDemandHistoryQuery(query, User), ct));
    [HttpPost("runs/{runId:guid}/rerun"), Authorize(Policy = "DemandRun")]
    public async Task<ActionResult<DemandRunAcceptedResponse>> Rerun(Guid runId, RerunDemandRunRequest request, CancellationToken ct) => Accepted(await mediator.Send(new RerunDemandRunCommand(runId, request, User, Actor, Correlation), ct));
    [HttpGet("runs/{runId:guid}/rows"), Authorize(Policy = "DemandOutputRead")]
    public async Task<ActionResult<PaginatedResponse<DemandRowResponse>>> Rows(Guid runId, [FromQuery] DemandRowsQuery query, CancellationToken ct) => Ok(await mediator.Send(new GetDemandRowsQuery(query with { RunId = runId }, User), ct));
    [HttpGet("batches"), Authorize(Policy = "DemandOutputRead")]
    public async Task<ActionResult<PaginatedResponse<DemandBatchResponse>>> Batches([FromQuery] DemandBatchesQuery query, CancellationToken ct) => Ok(await mediator.Send(new GetDemandBatchesQuery(query, User), ct));
    [HttpPost("batches/{batchId:guid}/spool"), Authorize(Policy = "DemandSpool")]
    public async Task<ActionResult<SpoolStatusResponse>> Spool(Guid batchId, SpoolBatchRequest request, CancellationToken ct) => Ok(await mediator.Send(new SpoolDemandBatchCommand(batchId, request, User, Actor, Source, Correlation), ct));
    [HttpGet("exports/output"), Authorize(Policy = "DemandPdfExport")]
    public async Task<IActionResult> ExportOutput([FromQuery] DemandPdfQuery query, CancellationToken ct) { var file = await exports.RenderOutputAsync(query, User, ct); return File(file.Content, file.ContentType, file.FileName); }
    [HttpGet("exports/batches"), Authorize(Policy = "DemandPdfExport")]
    public async Task<IActionResult> ExportBatches([FromQuery] DemandPdfQuery query, CancellationToken ct) { var file = await exports.RenderBatchesAsync(query, User, ct); return File(file.Content, file.ContentType, file.FileName); }
}
