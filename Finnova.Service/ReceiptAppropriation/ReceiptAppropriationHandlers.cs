using System.Security.Claims;
using Finnova.Models.Contracts.ReceiptAppropriation;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.ReceiptAppropriation.Ports;
using Finnova.Service.ReceiptAppropriation.Rules;
using FluentValidation;
using MediatR;
using ReceiptAppropriationEntity = Finnova.Models.Domain.Entities.ReceiptAppropriation;

namespace Finnova.Service.ReceiptAppropriation;

public sealed record ShowAppropriationQuery(Guid ReceiptId, int Page, int PageSize, ClaimsPrincipal User) : IRequest<ShowAppropriationResponse>;
public sealed record SuggestAppropriationQuery(Guid ReceiptId, ClaimsPrincipal User, string CorrelationId) : IRequest<AppropriationPreviewResponse>;
public sealed record ConfirmAppropriationCommand(Guid ReceiptId, ConfirmAppropriationRequest Request, ClaimsPrincipal User, string Actor, string CorrelationId) : IRequest<ConfirmedAppropriationResponse>;

public sealed class ShowAppropriationQueryValidator : AbstractValidator<ShowAppropriationQuery>
{
    public ShowAppropriationQueryValidator() { RuleFor(x => x.ReceiptId).NotEmpty(); RuleFor(x => x.Page).InclusiveBetween(1, 100000); RuleFor(x => x.PageSize).InclusiveBetween(1, 500); }
}
public sealed class SuggestAppropriationQueryValidator : AbstractValidator<SuggestAppropriationQuery> { public SuggestAppropriationQueryValidator() => RuleFor(x => x.ReceiptId).NotEmpty(); }
public sealed class ConfirmAppropriationCommandValidator : AbstractValidator<ConfirmAppropriationCommand>
{
    public ConfirmAppropriationCommandValidator() { RuleFor(x => x.ReceiptId).NotEmpty(); RuleFor(x => x.Request.ClientRequestId).NotEmpty(); RuleFor(x => x.Request.Mode).IsInEnum(); RuleForEach(x => x.Request.Allocations).ChildRules(a => a.RuleFor(x => x.DueId).NotEmpty()); }
}

internal static class AppropriationAccess
{
    public static async Task<(ReceiptContext Receipt, IReadOnlyList<PendingDueProjection> Dues)> LoadAsync(Guid id, ClaimsPrincipal user, IReceiptContextPort receipts, IPendingDuePort dues, IAppropriationScopeResolver scope, CancellationToken ct)
    {
        var receipt = await receipts.GetAsync(id, ct) ?? throw new ReceiptAppropriationNotFoundException("Receipt was not found or is not accessible.");
        if (!await receipts.CanAppropriateAsync(receipt, ct)) throw new ReceiptAppropriationConflictException("ERR-APPROP-VALIDATION", "The receipt is not in a status that permits appropriation.");
        var businessScope = await scope.ResolveAsync(user, ct); if (!scope.CanAccess(businessScope, receipt)) throw new ReceiptAppropriationForbiddenException("You are not authorized for this receipt appropriation scope.");
        var pending = await dues.GetEligibleAsync(receipt, ct); return (receipt, pending.Where(x => x.Status == "Collectible" && x.OutstandingAmount > 0m).ToArray());
    }
}

public sealed class ShowAppropriationQueryHandler(IReceiptContextPort receipts, IPendingDuePort dues, IAppropriationScopeResolver scope) : IRequestHandler<ShowAppropriationQuery, ShowAppropriationResponse>
{
    public async Task<ShowAppropriationResponse> Handle(ShowAppropriationQuery q, CancellationToken ct)
    {
        var (r, source) = await AppropriationAccess.LoadAsync(q.ReceiptId, q.User, receipts, dues, scope, ct); var ordered = AppropriationCalculator.Order(source); var items = ordered.Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).Select(x => new ReceiptAppropriationDueResponse(x.Id, x.AccountId, x.AccountNumber, x.DueType, x.DueDate, x.OutstandingAmount, 0m, x.OutstandingAmount, "INR", x.Version)).ToArray();
        var totalPages = Math.Max(1, (int)Math.Ceiling(ordered.Count / (double)q.PageSize)); return new(r.Id, r.CustomerId, r.CompanyId, r.LineOfBusinessId, r.Amount, items, ordered.Count, q.Page, q.PageSize, totalPages, new(r.Amount, 0m, 0m, r.Amount, true), r.Version, string.Join('|', ordered.Select(x => $"{x.Id}:{x.Version}")));
    }
}

public sealed class SuggestAppropriationQueryHandler(IReceiptContextPort receipts, IPendingDuePort dues, IAppropriationRulePort rules, IAppropriationScopeResolver scope) : IRequestHandler<SuggestAppropriationQuery, AppropriationPreviewResponse>
{
    public async Task<AppropriationPreviewResponse> Handle(SuggestAppropriationQuery q, CancellationToken ct)
    {
        var (r, source) = await AppropriationAccess.LoadAsync(q.ReceiptId, q.User, receipts, dues, scope, ct); var matches = await rules.GetActiveAsync(r.CompanyId, r.LineOfBusinessId, ct); if (matches.Count == 0) throw new ReceiptAppropriationRuleException("No active appropriation rule is configured."); if (matches.Count != 1) throw new ReceiptAppropriationRuleException("Multiple active appropriation rules match the receipt."); var rule = matches[0];
        CalculationResult calc; try { calc = AppropriationCalculator.Calculate(r.Amount, source, rule); } catch (InvalidOperationException ex) { throw new ReceiptAppropriationRuleException(ex.Message); }
        var lines = calc.Lines.Select(x => new ReceiptAppropriationDueResponse(x.DueId, x.AccountId, x.AccountNumber, x.DueType, x.DueDate, x.OutstandingAmount, x.AllocatedAmount, x.OutstandingAmount - x.AllocatedAmount, "INR", x.DueVersion, x.RuleDetailId, x.RuleOrder, x.TargetAmount)).ToArray(); var summary = new AppropriationRuleSummary(rule.Id, rule.Version, rule.Direction, rule.FullDue, rule.Details.OrderBy(x => x.Order).Select(x => new AppropriationRuleDetailResponse(x.Id, x.DueType, x.Percentage, x.Order)).ToArray()); return new(q.ReceiptId, r.CustomerId, r.CompanyId, r.LineOfBusinessId, r.Amount, AppropriationMode.Suggest, lines, summary, new(r.Amount, calc.DueTotal, calc.Residual, calc.Residual, calc.Reconciles), r.Version, rule.Version, string.Join('|', source.Select(x => $"{x.Id}:{x.Version}")), DateTime.UtcNow, q.CorrelationId);
    }
}

public sealed class ConfirmAppropriationCommandHandler(IReceiptContextPort receipts, IPendingDuePort dues, IAppropriationRulePort rules, IAdvanceAccountPort advance, IAppropriationScopeResolver scope, IReceiptAppropriationRepository repository, IApplicationUnitOfWork uow) : IRequestHandler<ConfirmAppropriationCommand, ConfirmedAppropriationResponse>
{
    public async Task<ConfirmedAppropriationResponse> Handle(ConfirmAppropriationCommand c, CancellationToken ct)
    {
        var existing = await repository.FindByClientRequestIdAsync(c.Request.ClientRequestId, ct); if (existing is not null) return Map(existing, c.CorrelationId);
        var (r, source) = await AppropriationAccess.LoadAsync(c.ReceiptId, c.User, receipts, dues, scope, ct); var byId = source.ToDictionary(x => x.Id); var rows = c.Request.Allocations.Where(x => x.AllocatedAmount > 0m).ToArray(); if (rows.Any(x => !byId.ContainsKey(x.DueId) || x.AllocatedAmount < 0m || AppropriationCalculator.Money(x.AllocatedAmount) != x.AllocatedAmount || x.AllocatedAmount > byId[x.DueId].OutstandingAmount)) throw new ReceiptAppropriationReconciliationException("An allocation exceeds the current pending due balance.");
        var dueTotal = AppropriationCalculator.Money(rows.Sum(x => x.AllocatedAmount)); if (dueTotal > r.Amount) throw new ReceiptAppropriationReconciliationException("Total allocated amount exceeds Receipt Document Amount"); var residual = AppropriationCalculator.Money(r.Amount - dueTotal); AdvanceAccountProjection? account = null; if (residual > 0m) { if (!c.Request.AcceptResidualTransfer) throw new ReceiptAppropriationAdvanceAccountException("Residual transfer to the Advance Account must be accepted."); account = await advance.GetActiveAsync(r.CompanyId, r.CustomerId, ct) ?? throw new ReceiptAppropriationAdvanceAccountException("The Advance Account is not configured."); }
        var entity = new ReceiptAppropriationEntity { ReceiptId = r.Id, ClientRequestId = c.Request.ClientRequestId, Mode = c.Request.Mode, ReceiptAmount = r.Amount, DueAllocationTotal = dueTotal, AdvanceAccountAmount = residual, ResidualAmount = residual, CustomerId = r.CustomerId, CompanyId = r.CompanyId, LineOfBusinessId = r.LineOfBusinessId, ConfirmedBy = c.Actor, ConfirmedAtUtc = DateTime.UtcNow };
        foreach (var row in rows) { var due = byId[row.DueId]; entity.Allocations.Add(new ReceiptAllocation { PendingDueId = due.Id, AccountId = due.AccountId, AccountNumber = due.AccountNumber, CustomerId = due.CustomerId, CashFlowDueType = due.DueType, DueDate = due.DueDate, AllocatedAmount = row.AllocatedAmount, OutstandingAmountAtConfirm = due.OutstandingAmount, DueRowVersionAtConfirm = due.Version }); }
        if (account is not null) entity.AdvanceAccountAllocation = new AdvanceAccountAllocation { AdvanceAccountId = account.Id, Amount = residual, CompanyId = r.CompanyId, CustomerId = r.CustomerId, Currency = "INR" };
        await using var tx = await uow.BeginTransactionAsync(ct); await dues.ReserveAsync(entity.Allocations.Select(x => new DueReservation(x.PendingDueId, x.AllocatedAmount, x.DueRowVersionAtConfirm)).ToArray(), ct); await repository.AddAsync(entity, ct); var audit = new AppropriationAuditEntry { ReceiptId = r.Id, ReceiptAppropriationId = entity.Id, ClientRequestId = entity.ClientRequestId, Mode = entity.Mode.ToString(), Actor = c.Actor, CorrelationId = c.CorrelationId, DueAllocationTotal = dueTotal, AdvanceAccountAmount = residual, ResidualAmount = residual }; await repository.AddAuditAsync(audit, ct); await uow.SaveChangesAsync(ct); return Map(entity, c.CorrelationId, audit.Id);
    }
    private static ConfirmedAppropriationResponse Map(ReceiptAppropriationEntity x, string correlation, Guid? auditId = null) => new(x.Id, x.ReceiptId, x.ClientRequestId, x.Mode, x.Allocations.Select(a => new ReceiptAppropriationDueResponse(a.PendingDueId, a.AccountId, a.AccountNumber, a.CashFlowDueType, a.DueDate, a.OutstandingAmountAtConfirm, a.AllocatedAmount, a.OutstandingAmountAtConfirm - a.AllocatedAmount, "INR", a.DueRowVersionAtConfirm)).ToArray(), new(x.ReceiptAmount, x.DueAllocationTotal, x.AdvanceAccountAmount, x.ResidualAmount, true), x.ConfirmedAtUtc, x.Status, x.ConfirmedBy, auditId, correlation);
}