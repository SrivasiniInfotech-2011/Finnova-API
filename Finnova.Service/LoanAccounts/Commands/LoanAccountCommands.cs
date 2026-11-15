using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Finnova.Models.Contracts.LoanAccounts;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.LoanAccounts.Rules;
using Finnova.Service.Mappers;

namespace Finnova.Service.LoanAccounts.Commands;

public sealed record UpdateLoanAccountCommand(Guid Id, string? AccountName, string? Status, string RowVersion, string Actor, string CorrelationId)
    : IRequest<LoanAccountResponse>;
public sealed record SubmitKycResolutionCommand(Guid AccountId, string Reason, Guid MakerUserId, string MakerName, string CorrelationId)
    : IRequest<KycResolutionRequestResponse>;
public sealed record DecideKycResolutionCommand(Guid AccountId, Guid RequestId, bool Approve, string Comment, Guid CheckerUserId, string CheckerName, string CorrelationId)
    : IRequest<LoanAccountResponse>;
public sealed record RequestKycRecheckCommand(Guid AccountId, string? Reason, string Actor, string CorrelationId) : IRequest<bool>;
public sealed record ReprocessAccountRequestCommand(Guid RequestId, string Reason, string Actor, string CorrelationId) : IRequest<bool>;

// ---------------------------------------------------------------- validators
public sealed class UpdateLoanAccountCommandValidator : AbstractValidator<UpdateLoanAccountCommand>
{
    public UpdateLoanAccountCommandValidator()
    {
        RuleFor(x => x.AccountName).Must(n => n!.Trim().Length is >= 1 and <= 150)
            .When(x => x.AccountName is not null).WithMessage("Account name must be 1 to 150 characters.");
        RuleFor(x => x.Status).Must(s => Enum.TryParse<AccountStatus>(s, true, out _))
            .When(x => x.Status is not null).WithMessage("Status is not valid.");
        RuleFor(x => x.RowVersion).NotNull().WithMessage("Row version is required.");
    }
}

public sealed class SubmitKycResolutionCommandValidator : AbstractValidator<SubmitKycResolutionCommand>
{
    public SubmitKycResolutionCommandValidator() =>
        RuleFor(x => x.Reason).Must(r => (r?.Trim().Length ?? 0) is >= KycResolutionRules.ReasonMin and <= KycResolutionRules.ReasonMax)
            .WithMessage("Reason must be 10 to 500 characters.");
}

public sealed class DecideKycResolutionCommandValidator : AbstractValidator<DecideKycResolutionCommand>
{
    public DecideKycResolutionCommandValidator()
    {
        RuleFor(x => x.Comment).Must(c => !string.IsNullOrWhiteSpace(c)).WithMessage("A comment is required.");
        RuleFor(x => x.Comment).MaximumLength(500).WithMessage("Comment must not exceed 500 characters.");
    }
}

public sealed class ReprocessAccountRequestCommandValidator : AbstractValidator<ReprocessAccountRequestCommand>
{
    public ReprocessAccountRequestCommandValidator() =>
        RuleFor(x => x.Reason).Must(r => (r?.Trim().Length ?? 0) is >= 1 and <= 500).WithMessage("Reason must be 1 to 500 characters.");
}

// ---------------------------------------------------------------- handlers
public sealed class UpdateLoanAccountCommandHandler : IRequestHandler<UpdateLoanAccountCommand, LoanAccountResponse>
{
    private readonly ILoanAccountRepository _accounts;
    private readonly IAccountAuditRepository _audit;
    private readonly IApplicationUnitOfWork _uow;
    private readonly AccountCreationKycOptions _options;

    public UpdateLoanAccountCommandHandler(ILoanAccountRepository accounts, IAccountAuditRepository audit,
        IApplicationUnitOfWork uow, IOptions<AccountCreationKycOptions> options)
        => (_accounts, _audit, _uow, _options) = (accounts, audit, uow, options.Value);

    public async Task<LoanAccountResponse> Handle(UpdateLoanAccountCommand r, CancellationToken ct)
    {
        var account = await _accounts.GetTrackedAsync(r.Id, ct) ?? throw LoanAccountNotFoundException.ForAccount(r.Id);
        if (!AccountStatusRules.IsUpdatable(account.Status))
            throw new LoanAccountConflictException("A closed account cannot be updated.");

        byte[] provided;
        try { provided = Convert.FromBase64String(r.RowVersion ?? string.Empty); }
        catch (FormatException) { throw new LoanAccountValidationException("The row version is not valid."); }
        if (!(account.RowVersion ?? Array.Empty<byte>()).AsSpan().SequenceEqual(provided))
            throw new LoanAccountConflictException("The account was changed by another request. Refresh and retry.");
        _uow.Context.Entry(account).Property(x => x.RowVersion).OriginalValue = provided;   // guards the race window (R8.18)

        // Validate first, mutate second, so a refused update leaves the Account unchanged (R8.14).
        AccountStatus? target = null;
        if (r.Status is not null)
        {
            var parsed = Enum.Parse<AccountStatus>(r.Status, true);
            if (parsed != account.Status)
            {
                if (!AccountStatusRules.IsTransitionAllowed(account.Status, parsed))
                    throw new LoanAccountConflictException($"A status change from {account.Status} to {parsed} is not permitted.");
                target = parsed;
            }
        }
        var newName = r.AccountName?.Trim();
        var nameChanged = newName is not null && newName != account.AccountName;

        if (target is null && !nameChanged)
            return await LoanAccountViewLoader.LoadAsync(_accounts, _options, r.Id, ct);   // no-op: UpdatedAt untouched (R8.17)

        var summary = new List<string>();
        if (nameChanged) { summary.Add("AccountName changed"); account.AccountName = newName!; }
        if (target is { } t) { summary.Add($"Status {account.Status}->{t}"); account.Status = t; }
        account.UpdatedAt = DateTime.UtcNow;   // Type, Currency, Balance, numbers, references and dates are never touched (R8.16)

        await _audit.AddAsync(LoanAccountViewLoader.Audit.User(account.Id, null,
            target is null ? "Updated" : "StatusChanged", r.Actor, r.CorrelationId, string.Join("; ", summary)), ct);
        await _uow.SaveChangesAsync(ct);
        return await LoanAccountViewLoader.LoadAsync(_accounts, _options, r.Id, ct);
    }
}

public sealed class SubmitKycResolutionCommandHandler : IRequestHandler<SubmitKycResolutionCommand, KycResolutionRequestResponse>
{
    private readonly ILoanAccountRepository _accounts;
    private readonly IAccountKycResolutionRepository _resolutions;
    private readonly IAccountAuditRepository _audit;
    private readonly IApplicationUnitOfWork _uow;

    public SubmitKycResolutionCommandHandler(ILoanAccountRepository accounts, IAccountKycResolutionRepository resolutions,
        IAccountAuditRepository audit, IApplicationUnitOfWork uow)
        => (_accounts, _resolutions, _audit, _uow) = (accounts, resolutions, audit, uow);

    public async Task<KycResolutionRequestResponse> Handle(SubmitKycResolutionCommand r, CancellationToken ct)
    {
        var account = await _accounts.GetTrackedAsync(r.AccountId, ct) ?? throw LoanAccountNotFoundException.ForAccount(r.AccountId);
        var hasPending = await _resolutions.GetPendingAsync(account.Id, ct) is not null;
        switch (KycResolutionRules.CheckSubmit(account.KycFlagged, hasPending, r.Reason))
        {
            case SubmitCheck.NotFlagged: throw new LoanAccountConflictException("The KYC flag is already cleared for this account.");
            case SubmitCheck.PendingExists: throw new LoanAccountConflictException("A KYC resolution request is already pending for this account.");
            case SubmitCheck.ReasonInvalid: throw new LoanAccountValidationException("Reason must be 10 to 500 characters.");
        }

        var request = new AccountKycResolutionRequest
        {
            AccountId = account.Id,
            Status = KycResolutionStatus.Pending,
            MakerUserId = r.MakerUserId,
            MakerName = r.MakerName,
            Reason = r.Reason.Trim(),
            PreviousKycStatus = account.KycStatus ?? KycStatus.Pending,
            SubmittedAtUtc = DateTime.UtcNow,
            CorrelationId = r.CorrelationId
        };
        await _resolutions.AddAsync(request, ct);
        await _audit.AddAsync(LoanAccountViewLoader.Audit.User(account.Id, request.Id, "KycResolutionSubmitted",
            r.MakerUserId.ToString(), r.CorrelationId, "KYC resolution requested."), ct);
        try { await _uow.SaveChangesAsync(ct); }
        catch (DbUpdateException)   // filtered unique index: two makers raced (R9.9)
        {
            throw new LoanAccountConflictException("A KYC resolution request is already pending for this account.");
        }
        return request.ToResponse();   // the Account's KycStatus and KycFlagged are unchanged (R9.1)
    }
}

public sealed class DecideKycResolutionCommandHandler : IRequestHandler<DecideKycResolutionCommand, LoanAccountResponse>
{
    private readonly ILoanAccountRepository _accounts;
    private readonly IAccountKycResolutionRepository _resolutions;
    private readonly IAccountKycHistoryRepository _history;
    private readonly IAccountAuditRepository _audit;
    private readonly IApplicationUnitOfWork _uow;
    private readonly AccountCreationKycOptions _options;

    public DecideKycResolutionCommandHandler(ILoanAccountRepository accounts, IAccountKycResolutionRepository resolutions,
        IAccountKycHistoryRepository history, IAccountAuditRepository audit, IApplicationUnitOfWork uow,
        IOptions<AccountCreationKycOptions> options)
        => (_accounts, _resolutions, _history, _audit, _uow, _options) = (accounts, resolutions, history, audit, uow, options.Value);

    public async Task<LoanAccountResponse> Handle(DecideKycResolutionCommand r, CancellationToken ct)
    {
        var request = await _resolutions.GetTrackedAsync(r.RequestId, ct);
        if (request is null || request.AccountId != r.AccountId)
            throw new LoanAccountNotFoundException($"KYC resolution request '{r.RequestId}' was not found.");

        switch (KycResolutionRules.CheckDecision(request.Status, request.MakerUserId, r.CheckerUserId, r.Comment))
        {
            case DecisionCheck.NotPending: throw new LoanAccountConflictException("Only a pending KYC resolution request can be decided.");
            case DecisionCheck.SameUser: throw new KycResolutionForbiddenException();   // four-eyes (R9.4)
            case DecisionCheck.CommentRequired: throw new LoanAccountValidationException("A comment is required.");
        }

        var account = await _accounts.GetTrackedAsync(r.AccountId, ct) ?? throw LoanAccountNotFoundException.ForAccount(r.AccountId);
        var now = DateTime.UtcNow;
        request.Status = r.Approve ? KycResolutionStatus.Approved : KycResolutionStatus.Rejected;
        request.CheckerUserId = r.CheckerUserId;
        request.CheckerName = r.CheckerName;
        request.DecisionComment = r.Comment.Trim();
        request.DecidedAtUtc = now;

        if (r.Approve)
        {
            var previous = account.KycStatus;
            KycResolutionRules.ApplyApproval(account, now);   // the only path to Passed/Manual
            await _history.AddAsync(new AccountKycHistoryEntry
            {
                AccountId = account.Id,
                Source = KycSource.Manual,
                KycStatus = KycStatus.Passed,
                PanValidation = account.KycPanValidation ?? KycCheckResult.NotChecked,        // automated results are retained
                PanNameMatch = account.KycPanNameMatch ?? KycCheckResult.NotChecked,
                AadhaarValidation = account.KycAadhaarValidation ?? KycCheckResult.NotChecked,
                AadhaarAddressMatch = account.KycAadhaarAddressMatch ?? KycCheckResult.NotChecked,
                PreviousStatus = previous,
                Reason = request.Reason,
                Comment = request.DecisionComment,
                ResolutionRequestId = request.Id,
                MakerUserId = request.MakerUserId,
                CheckerUserId = r.CheckerUserId,
                Actor = r.CheckerName,
                CorrelationId = r.CorrelationId,
                OccurredAtUtc = now
            }, ct);
        }

        await _audit.AddAsync(LoanAccountViewLoader.Audit.User(account.Id, request.Id,
            r.Approve ? "KycResolutionApproved" : "KycResolutionRejected", r.CheckerUserId.ToString(), r.CorrelationId,
            r.Approve ? "KYC resolution approved." : "KYC resolution rejected."), ct);
        await _uow.SaveChangesAsync(ct);   // DbUpdateConcurrencyException (two checkers) is mapped to 409 by the middleware
        return await LoanAccountViewLoader.LoadAsync(_accounts, _options, account.Id, ct);
    }
}

public sealed class RequestKycRecheckCommandHandler : IRequestHandler<RequestKycRecheckCommand, bool>
{
    private readonly ILoanAccountRepository _accounts;
    private readonly IAccountKycJobRepository _jobs;
    private readonly IAccountAuditRepository _audit;
    private readonly IApplicationUnitOfWork _uow;

    public RequestKycRecheckCommandHandler(ILoanAccountRepository accounts, IAccountKycJobRepository jobs,
        IAccountAuditRepository audit, IApplicationUnitOfWork uow)
        => (_accounts, _jobs, _audit, _uow) = (accounts, jobs, audit, uow);

    public async Task<bool> Handle(RequestKycRecheckCommand r, CancellationToken ct)
    {
        var account = await _accounts.GetTrackedAsync(r.AccountId, ct) ?? throw LoanAccountNotFoundException.ForAccount(r.AccountId);
        if (!account.KycFlagged) throw new LoanAccountConflictException("A KYC re-check is only available for flagged accounts.");

        var now = DateTime.UtcNow;
        var job = await _jobs.GetTrackedAsync(account.Id, ct);
        if (job is null)
        {
            await _jobs.AddAsync(new AccountKycJob { AccountId = account.Id, CorrelationId = r.CorrelationId, RecheckRequestedBy = r.Actor, RecheckReason = r.Reason }, ct);
        }
        else
        {
            job.Status = AccountRequestStatus.Pending;
            job.AttemptCount = 0;
            job.NextAttemptAtUtc = null;
            job.ClaimedBy = null;
            job.LeaseExpiresAtUtc = null;
            job.FailureReason = null;
            job.CorrelationId = r.CorrelationId;
            job.RecheckRequestedBy = r.Actor;
            job.RecheckReason = r.Reason;
            job.UpdatedAtUtc = now;
        }
        await _audit.AddAsync(LoanAccountViewLoader.Audit.User(account.Id, null, "KycRecheck", r.Actor, r.CorrelationId, "KYC re-check requested."), ct);
        await _uow.SaveChangesAsync(ct);
        return true;
    }
}

public sealed class ReprocessAccountRequestCommandHandler : IRequestHandler<ReprocessAccountRequestCommand, bool>
{
    private readonly IAccountCreationRequestRepository _requests;
    private readonly IAccountAuditRepository _audit;
    private readonly IApplicationUnitOfWork _uow;

    public ReprocessAccountRequestCommandHandler(IAccountCreationRequestRepository requests, IAccountAuditRepository audit, IApplicationUnitOfWork uow)
        => (_requests, _audit, _uow) = (requests, audit, uow);

    public async Task<bool> Handle(ReprocessAccountRequestCommand r, CancellationToken ct)
    {
        var request = await _requests.GetTrackedAsync(r.RequestId, ct)
                      ?? throw new LoanAccountNotFoundException($"Account creation request '{r.RequestId}' was not found.");
        if (!RequestRecoveryRules.CanReprocess(request.Status))
            throw new LoanAccountConflictException("Only a dead-lettered request can be reprocessed.");

        RequestRecoveryRules.ResetForReprocess(request, DateTime.UtcNow);
        await _audit.AddAsync(LoanAccountViewLoader.Audit.User(null, request.Id, "Reprocessed", r.Actor, r.CorrelationId,
            $"Reprocess requested: {r.Reason.Trim()}"), ct);
        await _uow.SaveChangesAsync(ct);
        return true;
    }
}
