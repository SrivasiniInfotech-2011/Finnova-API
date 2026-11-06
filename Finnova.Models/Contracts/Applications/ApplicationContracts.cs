using Finnova.Models.Domain.Enums;
using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.Lookups;
using Finnova.Models.Contracts.UserManagement;

namespace Finnova.Models.Contracts.Applications;

public sealed record CreateApplicationRequest(
    Guid LineOfBusinessId, Guid CustomerId, Guid BranchId, string ProductCode, string SchemeCode,
    decimal FinanceAmount, decimal IrrPercent, int Tenure, RepaymentFrequency RepaymentFrequency,
    DateOnly DisbursementDate, decimal? ProcessingCharges);

public sealed record UpdateApplicationRequest(
    Guid LineOfBusinessId, Guid CustomerId, Guid BranchId, string ProductCode, string SchemeCode,
    decimal FinanceAmount, decimal IrrPercent, int Tenure, RepaymentFrequency RepaymentFrequency,
    DateOnly DisbursementDate, decimal? ProcessingCharges);

public sealed record ApplicationResponse(
    Guid Id, string ApplicationNumber, Guid LineOfBusinessId, Guid CustomerId, Guid BranchId,
    string ProductCode, string SchemeCode, decimal FinanceAmount, decimal IrrPercent, int Tenure,
    RepaymentFrequency RepaymentFrequency, DateOnly DisbursementDate, decimal ProcessingCharges,
    ApplicationStatus Status, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);

public sealed record ApplicationInstallmentResponse(
    int InstallmentNumber, DateOnly DueDate, decimal OpeningBalance, decimal InterestPortion,
    decimal PrincipalPortion, decimal InstallmentAmount, decimal ClosingBalance);

public sealed record ApplicationScheduleResponse(
    Guid ApplicationId, bool IsFinalized, decimal FinanceAmount, decimal IrrPercent, int Tenure,
    RepaymentFrequency RepaymentFrequency, DateOnly DisbursementDate, decimal PeriodicInterestRate,
    decimal EmiAmount, List<ApplicationInstallmentResponse> Installments,
    decimal TotalInstallmentAmount, decimal TotalInterestAmount, decimal TotalPrincipalAmount);

public sealed record ProcessApplicationResponse(
    ApplicationResponse Application, ApplicationScheduleResponse Schedule);

public sealed record ApplicationReferenceOptionResponse(Guid Id, string Code, string Label);
public sealed record CustomerReferenceOptionResponse(Guid Id, string CustomerCode, string Label);
public sealed record ApplicationSearchRequest(
    string? Search, Guid? LineOfBusinessId, Guid? CustomerId, string? ProductCode,
    Guid? BranchId, string? SchemeCode, int Page = 1, int PageSize = 20);
public sealed record CustomerReferencePage(
    List<CustomerReferenceOptionResponse> Data, int Total, int Page, int PageSize, int TotalPages);
public sealed record ApplicationPage(
    List<ApplicationResponse> Data, int Total, int Page, int PageSize, int TotalPages);
public sealed record LookupReferenceRequest(string Module, string Type);
