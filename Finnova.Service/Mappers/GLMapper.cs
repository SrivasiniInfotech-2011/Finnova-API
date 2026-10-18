using Finnova.Models.Contracts.GL;
using Finnova.Models.Domain.Entities;

namespace Finnova.Service.Mappers;

public static class GLMapper
{
    public static GLAccountResponse ToResponse(this GLAccount x) => new(
        x.Id, x.CompanyId, x.GLCode, x.Name, x.Level, x.ParentGLId,
        x.AccountNature, x.NormalBalance, x.GLType, x.SubLedgerType, x.TaxType,
        x.IsPostable, x.CurrencyId, x.AllowManualJV, x.CostCentreMandatory,
        x.ProfitCentreMandatory, x.ReportingLine, x.EffectiveFrom, x.EffectiveTo,
        x.Status, x.AuthorizedVersionNumber,
        x.BranchApplicabilities.Select(b => b.BranchId).ToList(),
        x.CreatedAt, x.UpdatedAt);

    public static List<GLAccountResponse> ToResponseList(this IEnumerable<GLAccount> items)
        => items.Select(i => i.ToResponse()).ToList();

    public static GLAuditEntryResponse ToResponse(this GLAuditEntry a) => new(
        a.Id, a.GLAccountId, a.Action.ToString(), a.OldValues, a.NewValues, a.Summary,
        a.Remarks, a.ChangedBy, a.ChangedByRole, a.ChangedAtUtc);
}
