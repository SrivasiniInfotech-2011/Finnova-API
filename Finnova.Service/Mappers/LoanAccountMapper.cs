using Finnova.Models.Contracts.LoanAccounts;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Interfaces;
using Finnova.Service.LoanAccounts.Rules;

namespace Finnova.Service.Mappers;

public static class LoanAccountMapper
{
    public static LoanAccountListItemResponse ToListItem(this LoanAccountRow row, CustomerContact? contact)
    {
        var a = row.Account;
        return new LoanAccountListItemResponse(
            a.Id, row.ApplicationNumber, a.AccountNumber, a.Status.ToString(), row.CustomerCode,
            a.CustomerNameSnapshot ?? string.Empty,
            contact?.Mobile is null ? null : PiiMasker.Mask(contact.Mobile), contact?.City,
            (a.KycStatus ?? KycStatus.Pending).ToString(), a.KycFlagged,
            a.KycSource?.ToString(), a.KycSource?.ToString(), a.KycCheckedAtUtc,
            row.HasPendingResolution, a.OpenedAt);
    }

    public static KycCheckResultsResponse ToChecks(this LoanAccount a) => new(
        (a.KycPanValidation ?? KycCheckResult.NotChecked).ToString(),
        (a.KycPanNameMatch ?? KycCheckResult.NotChecked).ToString(),
        (a.KycAadhaarValidation ?? KycCheckResult.NotChecked).ToString(),
        (a.KycAadhaarAddressMatch ?? KycCheckResult.NotChecked).ToString());

    public static KycResolutionRequestResponse ToResponse(this AccountKycResolutionRequest r) => new(
        r.Id, r.AccountId, r.Status.ToString(), r.MakerUserId, r.MakerName, r.Reason, r.PreviousKycStatus.ToString(),
        r.SubmittedAtUtc, r.CheckerUserId, r.CheckerName, r.DecisionComment, r.DecidedAtUtc);

    public static KycHistoryEntryResponse ToResponse(this AccountKycHistoryEntry h) => new(
        h.Source.ToString(), h.KycStatus.ToString(),
        new KycCheckResultsResponse(h.PanValidation.ToString(), h.PanNameMatch.ToString(), h.AadhaarValidation.ToString(), h.AadhaarAddressMatch.ToString()),
        h.Reason, h.PreviousStatus?.ToString(), h.Actor, h.OccurredAtUtc);

    public static LoanAccountResponse ToResponse(this LoanAccountViewData v)
    {
        var a = v.Account;
        var pending = v.Resolutions.FirstOrDefault(r => r.Status == KycResolutionStatus.Pending);
        return new LoanAccountResponse(
            a.Id, a.ApplicationId ?? Guid.Empty, v.ApplicationNumber, a.AccountNumber, a.AccountName, a.Status.ToString(),
            a.CustomerId ?? Guid.Empty, v.Customer.CustomerCode, v.Customer.Name,
            v.Customer.Mobile is null ? null : PiiMasker.Mask(v.Customer.Mobile), v.Customer.City,
            v.Pan is null ? null : PiiMasker.Mask(v.Pan), v.Aadhaar is null ? null : PiiMasker.Mask(v.Aadhaar),
            a.LineOfBusinessId ?? Guid.Empty, a.BranchId ?? Guid.Empty, a.ProductCode ?? string.Empty, a.SchemeCode ?? string.Empty,
            v.Finance.FinanceAmount, v.Finance.IrrPercent, v.Finance.Tenure, v.Finance.RepaymentFrequency, v.Finance.DisbursementDate,
            (a.KycStatus ?? KycStatus.Pending).ToString(), a.KycFlagged, a.KycSource?.ToString(), a.KycCheckedAtUtc,
            a.ToChecks(), a.KycVerificationSessionId,
            v.History.Select(h => h.ToResponse()).ToList(),
            pending?.ToResponse(),
            v.Resolutions.Select(r => r.ToResponse()).ToList(),
            a.CreationNotes, a.OpenedAt, a.CreatedAt, a.UpdatedAt, Convert.ToBase64String(a.RowVersion ?? Array.Empty<byte>()));
    }
}
