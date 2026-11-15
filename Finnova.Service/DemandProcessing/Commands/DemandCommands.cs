using System.Security.Claims;
using FluentValidation;
using MediatR;
using Finnova.Models.Contracts.DemandProcessing;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Models.Domain.ValueObjects;
using Finnova.Repository.Interfaces;
using Finnova.Service.DemandProcessing.Configuration;
using Finnova.Service.DemandProcessing.Ports;
using Finnova.Service.DemandProcessing.Rules;
using Microsoft.Extensions.Options;

namespace Finnova.Service.DemandProcessing.Commands;

public sealed record StartDemandRunCommand(StartDemandRunRequest Request, ClaimsPrincipal Principal, string ActorId, DemandSource Source, string CorrelationId) : IRequest<DemandRunAcceptedResponse>;
public sealed record RerunDemandRunCommand(Guid RunId, RerunDemandRunRequest Request, ClaimsPrincipal Principal, string ActorId, string CorrelationId) : IRequest<DemandRunAcceptedResponse>;

public sealed class StartDemandRunValidator : AbstractValidator<StartDemandRunCommand>
{
    public StartDemandRunValidator(IOptions<DemandProcessingOptions> options)
    {
        RuleFor(x => x.Request.LineOfBusinessId).NotEmpty(); RuleFor(x => x.Request.PeriodYear).InclusiveBetween(2000, 2100); RuleFor(x => x.Request.PeriodMonth).InclusiveBetween(1, 12); RuleFor(x => x.Request.Frequency).Must(value => string.IsNullOrWhiteSpace(value) || options.Value.Frequencies.Contains(value.Trim(), StringComparer.Ordinal)).WithMessage("Frequency is not configured."); RuleFor(x => x.Request.IdempotencyKey).MaximumLength(200);
    }
}

public sealed class StartDemandRunHandler(IAccountSourcePort accounts, IAuthorizationScopeResolver scopes, IDemandRepository repository, IDemandWorkQueue queue, IOptions<DemandProcessingOptions> options, IClock clock) : IRequestHandler<StartDemandRunCommand, DemandRunAcceptedResponse>
{
    public async Task<DemandRunAcceptedResponse> Handle(StartDemandRunCommand command, CancellationToken ct)
    {
        var allowed = await scopes.ResolveAsync(command.Principal, ct); if (!allowed.LineOfBusinessIds.Contains(command.Request.LineOfBusinessId)) throw new UnauthorizedAccessException();
        var frequency = string.IsNullOrWhiteSpace(command.Request.Frequency) ? options.Value.DefaultFrequency : command.Request.Frequency.Trim();
        var branches = command.Request.BranchId.HasValue ? new[] { command.Request.BranchId.Value } : (await accounts.ResolveActiveBranchesAsync(command.Request.LineOfBusinessId, command.Principal, ct)).OrderBy(x => x).ToArray();
        if (branches.Length == 0) throw new DemandValidationException("No authorized Active Branches are available.", "ERR-DEMAND-NO-ACTIVE-BRANCH");
        if (branches.Any(x => !allowed.BranchIds.Contains(x))) throw new UnauthorizedAccessException();
        if (command.Request.BranchId.HasValue && !await accounts.IsActiveBranchAsync(command.Request.BranchId.Value, command.Request.LineOfBusinessId, ct)) throw new DemandValidationException("The selected Branch is deactivated.", "ERR-DEMAND-BRANCH-DEACTIVATED");
        var scope = DemandScope.Create(command.Request.LineOfBusinessId, branches); var period = DemandPeriod.Create(command.Request.PeriodYear, command.Request.PeriodMonth); var key = $"{scope.CanonicalValue}|{frequency}|{period.CanonicalValue}"; var fingerprint = DemandRules.Fingerprint(new DemandLogicalKey(scope, frequency, period, "pending"), "start", command.Request.IdempotencyKey);
        var existing = await repository.FindByLogicalScopeAsync(key, ct); if (existing is not null) { if (!string.Equals(existing.PayloadFingerprint, fingerprint, StringComparison.Ordinal)) throw new DemandIdempotencyConflictException(); return new(existing.Id, existing.CurrentRevisionNumber, existing.Status, existing.CorrelationId); }
        var run = new DemandRun { LineOfBusinessId = scope.LineOfBusinessId, Frequency = frequency, PeriodYear = period.Year, PeriodMonth = period.Month, BusinessTimezone = options.Value.BusinessTimezone, LogicalScopeKey = key, IdempotencyKey = command.Request.IdempotencyKey, PayloadFingerprint = fingerprint, Status = DemandRunStatus.Accepted, BillingStatus = BillingStageStatus.Pending, Source = command.Source, ActorId = command.ActorId, CorrelationId = command.CorrelationId, RequestedAtUtc = clock.UtcNow };
        run.BranchScopes = scope.BranchIds.Select((branch, index) => new DemandRunBranchScope { DemandRunId = run.Id, RevisionNumber = 1, BranchId = branch, SortOrder = index + 1 }).ToList(); run.Revisions.Add(new DemandRunRevision { DemandRunId = run.Id, RevisionNumber = 1, Status = DemandRunStatus.Accepted, DemandRun = run });
        await repository.AddAsync(run, ct); await repository.AddAuditAsync(new DemandAuditEntry { DemandRunId = run.Id, RevisionNumber = 1, Scope = key, ActorId = command.ActorId, Source = command.Source, CorrelationId = command.CorrelationId, NewStatus = run.Status.ToString(), OccurredAtUtc = clock.UtcNow, OutcomeCode = "RUN_ACCEPTED", SafeReferences = $"run={run.Id:N}" }, ct); await repository.SaveAsync(ct); await queue.EnqueueAsync(run.Id, ct); return new(run.Id, 1, run.Status, run.CorrelationId);
    }
}

public sealed class RerunDemandRunHandler(IDemandRepository repository, IDemandWorkQueue queue, IClock clock) : IRequestHandler<RerunDemandRunCommand, DemandRunAcceptedResponse>
{
    public async Task<DemandRunAcceptedResponse> Handle(RerunDemandRunCommand command, CancellationToken ct)
    {
        var run = await repository.GetRunAsync(command.RunId, ct) ?? throw new DemandNotFoundException(command.RunId); if (string.IsNullOrWhiteSpace(command.Request.Reason) || string.IsNullOrWhiteSpace(command.Request.IdempotencyKey)) throw new DemandValidationException("Rerun reason and idempotency key are required.");
        var revision = new DemandRunRevision { DemandRunId = run.Id, RevisionNumber = run.CurrentRevisionNumber + 1, SupersedesRevisionNumber = run.CurrentRevisionNumber, Status = DemandRunStatus.Accepted, DemandRun = run }; run.CurrentRevisionNumber = revision.RevisionNumber; run.Status = DemandRunStatus.Superseded; run.Revisions.Add(revision); await repository.AddAuditAsync(new DemandAuditEntry { DemandRunId = run.Id, RevisionNumber = revision.RevisionNumber, Scope = run.LogicalScopeKey, ActorId = command.ActorId, Source = DemandSource.User, CorrelationId = command.CorrelationId, PreviousStatus = DemandRunStatus.Superseded.ToString(), NewStatus = DemandRunStatus.Accepted.ToString(), OccurredAtUtc = clock.UtcNow, OutcomeCode = "RERUN_ACCEPTED", SafeReferences = $"run={run.Id:N};supersedes={revision.SupersedesRevisionNumber}" }, ct); await repository.SaveAsync(ct); await queue.EnqueueAsync(run.Id, ct); return new(run.Id, revision.RevisionNumber, revision.Status, command.CorrelationId);
    }
}
