using System.Security.Claims;
using MediatR;
using Finnova.Models.Contracts.DemandProcessing;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.DemandProcessing.Ports;

namespace Finnova.Service.DemandProcessing.Commands;

public sealed record SpoolDemandBatchCommand(Guid BatchId, SpoolBatchRequest Request, ClaimsPrincipal Principal, string ActorId, DemandSource Source, string CorrelationId) : IRequest<SpoolStatusResponse>;

public sealed class SpoolDemandBatchHandler(IDemandRepository repository, IAuthorizationScopeResolver scopes, IClock clock) : IRequestHandler<SpoolDemandBatchCommand, SpoolStatusResponse>
{
    public async Task<SpoolStatusResponse> Handle(SpoolDemandBatchCommand command, CancellationToken ct)
    {
        var allowed = await scopes.ResolveAsync(command.Principal, ct); var batch = await repository.GetBatchAsync(command.BatchId, allowed.BranchIds, ct) ?? throw new DemandNotFoundException(command.BatchId);
        if (batch.Status == DemandBatchStatus.Spooled) return new(batch.Id, batch.Status, command.CorrelationId);
        if (batch.Status != DemandBatchStatus.ReadyToSpool) throw new DemandSpoolNotReadyException();
        batch.Status = DemandBatchStatus.Spooled; batch.LastSpoolActorId = command.ActorId; batch.LastSpoolSource = command.Source; batch.LastSpooledAtUtc = clock.UtcNow;
        await repository.AddAuditAsync(new DemandAuditEntry { DemandRunId = batch.DemandRunId, RevisionNumber = batch.RevisionNumber, BranchId = batch.BranchId, DemandBatchId = batch.Id, Scope = $"lob={batch.LineOfBusinessId:N};branch={batch.BranchId:N}", ActorId = command.ActorId, Source = command.Source, CorrelationId = command.CorrelationId, PreviousStatus = DemandBatchStatus.ReadyToSpool.ToString(), NewStatus = DemandBatchStatus.Spooled.ToString(), OccurredAtUtc = clock.UtcNow, OutcomeCode = "BATCH_SPOOLED", SafeReferences = $"batch={batch.Id:N};run={batch.DemandRunId:N}" }, ct); await repository.SaveAsync(ct); return new(batch.Id, batch.Status, command.CorrelationId);
    }
}
