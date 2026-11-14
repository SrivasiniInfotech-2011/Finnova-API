using FluentValidation;
using MediatR;
using Finnova.Models.Contracts.Applications;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Models.Domain.ValueObjects;
using Finnova.Repository.Interfaces;
using Finnova.Service.Applications.Financial;
using Finnova.Service.Applications.Ports;
using Finnova.Service.Mappers;

namespace Finnova.Service.Applications.Commands;

public sealed record CreateApplicationCommand(Guid LineOfBusinessId, Guid CustomerId, Guid BranchId, string ProductCode,
    string SchemeCode, decimal FinanceAmount, decimal IrrPercent, int Tenure, RepaymentFrequency RepaymentFrequency,
    DateOnly DisbursementDate, decimal? ProcessingCharges, string ActingUser) : IRequest<ApplicationResponse>;

public sealed record UpdateApplicationCommand(Guid Id, Guid LineOfBusinessId, Guid CustomerId, Guid BranchId, string ProductCode,
    string SchemeCode, decimal FinanceAmount, decimal IrrPercent, int Tenure, RepaymentFrequency RepaymentFrequency,
    DateOnly DisbursementDate, decimal? ProcessingCharges, string ActingUser) : IRequest<ApplicationResponse>;

public sealed record PreviewApplicationScheduleQuery(Guid Id) : IRequest<ApplicationScheduleResponse>;
public sealed record ProcessApplicationCommand(Guid Id, string ActingUser) : IRequest<ProcessApplicationResponse>;

public sealed class CreateApplicationCommandHandler : IRequestHandler<CreateApplicationCommand, ApplicationResponse>
{
    private readonly IApplicationRepository _apps; private readonly IApplicationUnitOfWork _uow; private readonly IApplicationNumberGenerator _numbers;
    private readonly IApplicationReferenceValidator _references;
    public CreateApplicationCommandHandler(IApplicationRepository apps, IApplicationUnitOfWork uow, IApplicationNumberGenerator numbers, IApplicationReferenceValidator references)
        => (_apps, _uow, _numbers, _references) = (apps, uow, numbers, references);
    public async Task<ApplicationResponse> Handle(CreateApplicationCommand r, CancellationToken ct)
    {
        await _references.ValidateAsync(r.LineOfBusinessId, r.CustomerId, r.BranchId, r.ProductCode, r.SchemeCode, ct);
        var entity = new Application
        {
            ApplicationNumber = _numbers.Next(),
            LineOfBusinessId = r.LineOfBusinessId,
            CustomerId = r.CustomerId,
            BranchId = r.BranchId,
            ProductCode = r.ProductCode.Trim(),
            SchemeCode = r.SchemeCode.Trim(),
            FinanceAmount = r.FinanceAmount,
            IrrPercent = r.IrrPercent,
            Tenure = r.Tenure,
            RepaymentFrequency = r.RepaymentFrequency,
            DisbursementDate = r.DisbursementDate,
            ProcessingCharges = r.ProcessingCharges ?? 0m,
            Status = ApplicationStatus.Draft
        };
        await _apps.AddAsync(entity, ct); return entity.ToResponse();
    }
}

public sealed class UpdateApplicationCommandHandler : IRequestHandler<UpdateApplicationCommand, ApplicationResponse>
{
    private readonly IApplicationRepository _apps; private readonly IApplicationUnitOfWork _uow; private readonly IApplicationReferenceValidator _references;
    public UpdateApplicationCommandHandler(IApplicationRepository apps, IApplicationUnitOfWork uow, IApplicationReferenceValidator references)
        => (_apps, _uow, _references) = (apps, uow, references);
    public async Task<ApplicationResponse> Handle(UpdateApplicationCommand r, CancellationToken ct)
    {
        var a = await _apps.GetTrackedAsync(r.Id, ct) ?? throw new ApplicationNotFoundException(r.Id);
        if (a.Status == ApplicationStatus.Processed) throw new ApplicationAlreadyProcessedException();
        await _references.ValidateAsync(r.LineOfBusinessId, r.CustomerId, r.BranchId, r.ProductCode, r.SchemeCode, ct);
        var charges = r.ProcessingCharges ?? 0m;
        if (a.LineOfBusinessId == r.LineOfBusinessId && a.CustomerId == r.CustomerId && a.BranchId == r.BranchId && a.ProductCode == r.ProductCode.Trim()
            && a.SchemeCode == r.SchemeCode.Trim() && a.FinanceAmount == r.FinanceAmount && a.IrrPercent == r.IrrPercent && a.Tenure == r.Tenure
            && a.RepaymentFrequency == r.RepaymentFrequency && a.DisbursementDate == r.DisbursementDate && a.ProcessingCharges == charges) return a.ToResponse();
        a.LineOfBusinessId = r.LineOfBusinessId; a.CustomerId = r.CustomerId; a.BranchId = r.BranchId; a.ProductCode = r.ProductCode.Trim();
        a.SchemeCode = r.SchemeCode.Trim(); a.FinanceAmount = r.FinanceAmount; a.IrrPercent = r.IrrPercent; a.Tenure = r.Tenure;
        a.RepaymentFrequency = r.RepaymentFrequency; a.DisbursementDate = r.DisbursementDate; a.ProcessingCharges = charges; a.UpdatedAtUtc = DateTime.UtcNow;
        await _apps.UpdateAsync(a, ct); return a.ToResponse();
    }
}

public sealed class PreviewApplicationScheduleQueryHandler : IRequestHandler<PreviewApplicationScheduleQuery, ApplicationScheduleResponse>
{
    private readonly IApplicationRepository _apps; private readonly IApplicationScheduleRepository _schedules; private readonly AmortizationCalculator _calculator;
    public PreviewApplicationScheduleQueryHandler(IApplicationRepository apps, IApplicationScheduleRepository schedules, AmortizationCalculator calculator)
        => (_apps, _schedules, _calculator) = (apps, schedules, calculator);
    public async Task<ApplicationScheduleResponse> Handle(PreviewApplicationScheduleQuery r, CancellationToken ct)
    {
        var a = await _apps.GetByIdAsync(r.Id, ct) ?? throw new ApplicationNotFoundException(r.Id);
        if (a.Status == ApplicationStatus.Processed) { var stored = await _schedules.GetByApplicationIdAsync(r.Id, ct) ?? throw new ApplicationConflictException("Final schedule is missing."); return stored.ToResponse(); }
        var terms = new ApplicationTerms(a.FinanceAmount, a.IrrPercent, a.Tenure, a.RepaymentFrequency, a.DisbursementDate);
        return _calculator.Calculate(terms).ToResponse(a.Id, terms);
    }
}

public sealed class ProcessApplicationCommandHandler : IRequestHandler<ProcessApplicationCommand, ProcessApplicationResponse>
{
    private readonly IApplicationUnitOfWork _uow; private readonly IApplicationReferenceValidator _references; private readonly AmortizationCalculator _calculator;
    private readonly IAccountCreationRequestRepository _requests;
    public ProcessApplicationCommandHandler(IApplicationRepository apps, IApplicationScheduleRepository schedules, IApplicationStatusAuditRepository audit,
        IApplicationUnitOfWork uow, IApplicationReferenceValidator references, AmortizationCalculator calculator, IAccountCreationRequestRepository requests)
        => (_apps, _schedules, _audit, _uow, _references, _calculator, _requests) = (apps, schedules, audit, uow, references, calculator, requests);
    public async Task<ProcessApplicationResponse> Handle(ProcessApplicationCommand r, CancellationToken ct)
    {
        await using var transaction = await _uow.BeginTransactionAsync(ct);
        var a = await _apps.GetTrackedAsync(r.Id, ct) ?? throw new ApplicationNotFoundException(r.Id);
        if (a.Status == ApplicationStatus.Processed) throw new ApplicationAlreadyProcessedException();
        await _references.ValidateAsync(a.LineOfBusinessId, a.CustomerId, a.BranchId, a.ProductCode, a.SchemeCode, ct);
        var terms = new ApplicationTerms(a.FinanceAmount, a.IrrPercent, a.Tenure, a.RepaymentFrequency, a.DisbursementDate);
        var result = _calculator.Calculate(terms);
        var schedule = new ApplicationSchedule
        {
            ApplicationId = a.Id,
            IsFinalized = true,
            GeneratedAtUtc = DateTime.UtcNow,
            FinanceAmount = terms.FinanceAmount,
            IrrPercent = terms.IrrPercent,
            Tenure = terms.Tenure,
            RepaymentFrequency = terms.RepaymentFrequency,
            DisbursementDate = terms.DisbursementDate,
            PeriodicInterestRate = result.PeriodicInterestRate,
            EmiAmount = result.EmiAmount,
            TotalInstallmentAmount = result.TotalInstallmentAmount,
            TotalInterestAmount = result.TotalInterestAmount,
            TotalPrincipalAmount = result.TotalPrincipalAmount,
            Installments = result.Installments.Select(x => new ApplicationInstallment
            {
                InstallmentNumber = x.InstallmentNumber,
                DueDate = x.DueDate,
                OpeningBalance = x.OpeningBalance,
                InterestPortion = x.InterestPortion,
                PrincipalPortion = x.PrincipalPortion,
                InstallmentAmount = x.InstallmentAmount,
                ClosingBalance = x.ClosingBalance
            }).ToList()
        };
        await _schedules.AddAsync(schedule, ct); var previous = a.Status; a.Status = ApplicationStatus.Processed; a.UpdatedAtUtc = DateTime.UtcNow;
        await _audit.AddAsync(new ApplicationStatusAuditEntry { ApplicationId = a.Id, FromStatus = previous, ToStatus = a.Status, ChangedBy = r.ActingUser }, ct);
        // Transactional outbox: the Account Creation Request commits (or rolls back) with the Processed status (R1.1, R1.3).
        // The unique index on ApplicationId rejects a second request for the same Application (R1.4).
        await _requests.AddAsync(new AccountCreationRequest
        {
            ApplicationId = a.Id,
            ApplicationNumber = a.ApplicationNumber,
            CorrelationId = Guid.NewGuid().ToString("N"),
            InitiatedByUserId = Guid.TryParse(r.ActingUser, out var initiator) ? initiator : null,
            Status = AccountRequestStatus.Pending,
            AttemptCount = 0
        }, ct);
        await _uow.SaveChangesAsync(ct); await ((dynamic)transaction).CommitAsync(ct);
        return new ProcessApplicationResponse(a.ToResponse(), schedule.ToResponse());
    }
}
