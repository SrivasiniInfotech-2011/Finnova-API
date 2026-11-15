using System.Security.Claims;
using MediatR;
using Finnova.Models.Contracts.DemandProcessing;
using Finnova.Repository.Interfaces;
using Finnova.Service.DemandProcessing.Ports;
using Finnova.Service.DemandProcessing.Queries;

namespace Finnova.Service.DemandProcessing.PdfExport;

public sealed class DemandPdfExportService(IMediator mediator, IPdfRenderer renderer, IClock clock) : IDemandPdfExportService
{
    public async Task<PdfFileResult> RenderOutputAsync(DemandPdfQuery query, ClaimsPrincipal principal, CancellationToken ct)
    {
        var rows = await mediator.Send(new GetDemandRowsQuery(new DemandRowsQuery(query.RunId, query.RevisionNumber, query.PeriodYear, query.PeriodMonth, query.LineOfBusinessId, query.BranchId, query.AccountId, query.CustomerId, query.RowStatus, 1, query.PageSize), principal), ct); var runId = query.RunId ?? rows.Data.FirstOrDefault()?.RunId ?? throw new Models.Domain.Exceptions.DemandPdfExportException("No authorized persisted output matched the export filters."); var run = await mediator.Send(new GetDemandRunQuery(runId, principal), ct); var document = new DemandPdfDocument("Demand Generation Output", clock.UtcNow, run, rows.Data, Array.Empty<DemandBatchResponse>(), rows.Data.Sum(x => x.TotalDemand)); return new PdfFileResult(renderer.Render(document), "application/pdf", $"demand-output-{run.RunId:N}-{run.RevisionNumber}.pdf");
    }
    public async Task<PdfFileResult> RenderBatchesAsync(DemandPdfQuery query, ClaimsPrincipal principal, CancellationToken ct)
    {
        var batches = await mediator.Send(new GetDemandBatchesQuery(new DemandBatchesQuery(query.RunId, query.RevisionNumber, query.PeriodYear, query.PeriodMonth, query.LineOfBusinessId, query.BranchId, null, 1, query.PageSize), principal), ct); var runId = query.RunId ?? batches.Data.FirstOrDefault()?.RunId ?? throw new Models.Domain.Exceptions.DemandPdfExportException("No authorized persisted batches matched the export filters."); var run = await mediator.Send(new GetDemandRunQuery(runId, principal), ct); var document = new DemandPdfDocument("Demand Spooling Batches", clock.UtcNow, run, Array.Empty<DemandRowResponse>(), batches.Data, batches.Data.Sum(x => x.TotalDemand)); return new PdfFileResult(renderer.Render(document), "application/pdf", $"demand-batches-{run.RunId:N}-{run.RevisionNumber}.pdf");
    }
}
