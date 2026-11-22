using Finnova.Models.Contracts.AppropriationLogic;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.AppropriationLogic.Ports;
using Finnova.Service.AppropriationLogic.Rules;
using FluentValidation;
using MediatR;
using System.Security.Claims;
using AppropriationRepository = Finnova.Repository.Interfaces.IAppropriationLogicRepository;

namespace Finnova.Service.AppropriationLogic;

public sealed record ListAppropriationLogicQuery(Guid? CompanyId, Guid? LineOfBusinessId, bool IncludeInactive, ClaimsPrincipal User) : IRequest<IReadOnlyList<AppropriationLogicListItem>>;
public sealed record GetAppropriationLogicQuery(Guid RuleId, ClaimsPrincipal User) : IRequest<AppropriationLogicResponse>;
public sealed record CreateAppropriationLogicCommand(CreateAppropriationLogicRequest Request, ClaimsPrincipal User, string Actor, string CorrelationId) : IRequest<AppropriationLogicResponse>;
public sealed record UpdateAppropriationLogicCommand(Guid RuleId, UpdateAppropriationLogicRequest Request, ClaimsPrincipal User, string Actor, string CorrelationId) : IRequest<AppropriationLogicResponse>;
public sealed record ActivateAppropriationLogicCommand(Guid RuleId, LifecycleAppropriationLogicRequest Request, ClaimsPrincipal User, string Actor, string CorrelationId) : IRequest<AppropriationLogicResponse>;
public sealed record DeactivateAppropriationLogicCommand(Guid RuleId, LifecycleAppropriationLogicRequest Request, ClaimsPrincipal User, string Actor, string CorrelationId) : IRequest<AppropriationLogicResponse>;
public sealed record ResolveAppropriationLogicQuery(Guid CompanyId, Guid LineOfBusinessId) : IRequest<AppropriationResolutionResponse>;

public sealed class CreateAppropriationLogicValidator : AbstractValidator<CreateAppropriationLogicCommand>
{
    public CreateAppropriationLogicValidator() { RuleFor(x => x.Request.CompanyId).NotEmpty(); RuleFor(x => x.Request.IdempotencyKey).NotEmpty().MaximumLength(100); RuleFor(x => x.Request.Details).NotEmpty(); RuleForEach(x => x.Request.Details).ChildRules(d => d.RuleFor(y => y.Percentage).InclusiveBetween(0m, 100m)); }
}

public sealed class UpdateAppropriationLogicValidator : AbstractValidator<UpdateAppropriationLogicCommand>
{
    public UpdateAppropriationLogicValidator() { RuleFor(x => x.RuleId).NotEmpty(); RuleFor(x => x.Request.ExpectedVersion).GreaterThan(0); RuleFor(x => x.Request.IdempotencyKey).NotEmpty().MaximumLength(100); RuleFor(x => x.Request.Details).NotEmpty(); }
}

public sealed class LifecycleAppropriationLogicValidator : AbstractValidator<LifecycleAppropriationLogicCommandBase>
{
    public LifecycleAppropriationLogicValidator() { RuleFor(x => x.RuleId).NotEmpty(); RuleFor(x => x.Request.ExpectedVersion).GreaterThan(0); RuleFor(x => x.Request.IdempotencyKey).NotEmpty().MaximumLength(100); }
}

public abstract record LifecycleAppropriationLogicCommandBase(Guid RuleId, LifecycleAppropriationLogicRequest Request, ClaimsPrincipal User, string Actor, string CorrelationId);

internal static class AppropriationLogicMapper
{
    public static AppropriationLogicResponse Map(AppropriationRuleVersion x, string correlation) => new(x.RuleId, x.Id, x.CompanyId, x.LineOfBusinessId, x.Direction, x.FullDue, x.IsActive, x.VersionNumber, x.Details.OrderBy(d => d.SequenceNumber).Select(d => new AppropriationLogicDetailResponse(d.Id, d.SequenceNumber, d.DueType, d.CashFlowDueTypeCode, d.Percentage)).ToArray(), x.CreatedBy, x.CreatedAtUtc, x.ActivatedAtUtc, x.DeactivatedAtUtc, correlation);
}

public sealed class ListAppropriationLogicHandler(AppropriationRepository repository, IAppropriationAuthorizationPort authorization) : IRequestHandler<ListAppropriationLogicQuery, IReadOnlyList<AppropriationLogicListItem>>
{
    public async Task<IReadOnlyList<AppropriationLogicListItem>> Handle(ListAppropriationLogicQuery request, CancellationToken ct)
    {
        var scope = await authorization.ResolveAsync(request.User, ct);
        if (request.CompanyId.HasValue && !authorization.CanAdminister(scope, request.CompanyId.Value)) throw new AppropriationLogicForbiddenException("The caller cannot administer the requested Company.");
        var rows = await repository.ListAsync(request.CompanyId, request.LineOfBusinessId, request.IncludeInactive, ct);
        return rows.Select(x => new AppropriationLogicListItem(x.RuleId, x.Id, x.CompanyId, x.LineOfBusinessId, x.Direction, x.FullDue, x.IsActive, x.VersionNumber, x.Details.Sum(d => d.Percentage), x.CreatedAtUtc)).ToArray();
    }
}

public sealed class GetAppropriationLogicHandler(AppropriationRepository repository, IAppropriationAuthorizationPort authorization) : IRequestHandler<GetAppropriationLogicQuery, AppropriationLogicResponse>
{
    public async Task<AppropriationLogicResponse> Handle(GetAppropriationLogicQuery request, CancellationToken ct)
    {
        var rule = await repository.GetAsync(request.RuleId, ct) ?? throw new AppropriationLogicNotFoundException("Appropriation rule was not found.");
        var scope = await authorization.ResolveAsync(request.User, ct);
        if (!authorization.CanAdminister(scope, rule.CompanyId)) throw new AppropriationLogicForbiddenException("The caller cannot administer the rule Company.");
        return AppropriationLogicMapper.Map(rule, string.Empty);
    }
}

public sealed class CreateAppropriationLogicHandler(Repository.Interfaces.IAppropriationLogicRepository repository, IAppropriationReferencePort references, IAppropriationAuthorizationPort authorization, IAppropriationLogicUnitOfWork uow) : IRequestHandler<CreateAppropriationLogicCommand, AppropriationLogicResponse>
{
    public async Task<AppropriationLogicResponse> Handle(CreateAppropriationLogicCommand command, CancellationToken ct)
    {
        var prior = await repository.FindIdempotencyAsync(command.Request.IdempotencyKey, ct);
        if (prior is not null) throw new AppropriationLogicConflictException(AppropriationLogicErrorCodes.IdempotencyConflict, "The idempotency key has already been used.");
        var scope = await authorization.ResolveAsync(command.User, ct);
        if (!authorization.CanAdminister(scope, command.Request.CompanyId)) throw new AppropriationLogicForbiddenException("The caller cannot administer the requested Company.");
        if (!await references.IsActiveCompanyAsync(command.Request.CompanyId, ct)) throw new AppropriationLogicValidationException("Company is inactive or was not found.");
        var normalized = await AppropriationLogicNormalizer.NormalizeAsync(command.Request.CompanyId, command.Request.LineOfBusinessId, command.Request.Direction, command.Request.FullDue, command.Request.Details, references, ct);
        var entity = new AppropriationRuleVersion { CompanyId = normalized.CompanyId, LineOfBusinessId = normalized.LineOfBusinessId, Direction = normalized.Direction, FullDue = normalized.FullDue, IsActive = command.Request.Active, VersionNumber = 1, IdempotencyKey = command.Request.IdempotencyKey, NormalizedRequestHash = normalized.Hash, CreatedBy = command.Actor, CreatedAtUtc = DateTime.UtcNow };
        entity.Details = normalized.Details.Select(x => new AppropriationRuleDetail { RuleVersionId = entity.Id, SequenceNumber = x.SequenceNumber, DueType = x.DueType, CashFlowDueTypeCode = x.CashFlowDueTypeCode, Percentage = x.Percentage }).ToList();
        if (entity.IsActive) { await repository.DeactivateScopeAsync(entity.CompanyId, entity.LineOfBusinessId, ct); entity.ActivatedAtUtc = DateTime.UtcNow; }
        var audit = new AppropriationRuleAuditEntry { RuleId = entity.RuleId, RuleVersionId = entity.Id, CompanyId = entity.CompanyId, LineOfBusinessId = entity.LineOfBusinessId, Operation = AppropriationRuleOperation.Created, Actor = command.Actor, CorrelationId = command.CorrelationId, IdempotencyKey = command.Request.IdempotencyKey, NormalizedRequestHash = normalized.Hash };
        await using var tx = await uow.BeginTransactionAsync(ct); await repository.AddVersionAsync(entity, ct); await repository.AddAuditAsync(audit, ct); await repository.AddIdempotencyAsync(new RuleMutationIdempotency { IdempotencyKey = command.Request.IdempotencyKey, Operation = "Created", NormalizedRequestHash = normalized.Hash, RuleId = entity.RuleId, RuleVersionId = entity.Id }, ct); await uow.SaveChangesAsync(ct); return AppropriationLogicMapper.Map(entity, command.CorrelationId);
    }
}

public sealed class ResolveAppropriationLogicHandler(IAppropriationRuleResolver resolver) : IRequestHandler<ResolveAppropriationLogicQuery, AppropriationResolutionResponse>
{
    public async Task<AppropriationResolutionResponse> Handle(ResolveAppropriationLogicQuery request, CancellationToken ct)
    {
        var result = await resolver.ResolveAsync(request.CompanyId, request.LineOfBusinessId, ct);
        if (result.Status == AppropriationResolutionStatus.NotFound) throw new AppropriationLogicNotFoundException("No active appropriation rule is configured for the Company and Line of Business.");
        if (result.Status == AppropriationResolutionStatus.Ambiguous) throw new AppropriationLogicConflictException(AppropriationLogicErrorCodes.Ambiguous, "Multiple active appropriation rules match the Company and Line of Business.");
        var x = result.Resolution!.Rule;
        return new(result.ResolverContractVersion, x.RuleId, x.Id, x.VersionNumber, x.CompanyId, x.LineOfBusinessId, result.Resolution.UsedGlobalFallback, x.Direction, x.FullDue, x.Details.OrderBy(d => d.SequenceNumber).Select(d => new AppropriationResolutionDetail(d.Id, d.DueType, d.CashFlowDueTypeCode, d.Percentage, d.SequenceNumber)).ToArray());
    }
}