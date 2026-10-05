using Finnova.Models.Contracts.DraweeBanks;
using Finnova.Models.Domain.Entities;

namespace Finnova.Service.Mappers;

public static class DraweeBankMapper
{
    public static DraweeBranchResponse ToResponse(this DraweeBranch b) =>
        new(b.Id, b.DraweeBankId, b.PlaceCode, b.PlaceName, b.Address, b.PostalCode, b.StartDate, b.EndDate);

    public static RestrictionDetailResponse ToResponse(this RestrictionDetail r) =>
        new(r.Id, r.DraweeBankId, r.ClearingDays, r.StartDate, r.EndDate);

    // Standalone rule mapper used by the challan-rule endpoints (read/create/update).
    public static ChallanRuleResponse ToResponse(this ChallanRule c) =>
        new(c.Id, c.DraweeBankId, c.RuleCode, c.FormatPattern, c.ValidationExpression, c.RoutingTarget,
            c.IsActive, c.CreatedAt, c.UpdatedAt);

    // Bank response carries owned children only — challan rules are decoupled and never mapped here.
    public static DraweeBankResponse ToResponse(this Finnova.Models.Domain.Entities.DraweeBank x) =>
        new(x.Id, x.BankCode, x.BankName, x.IsActive,
            x.Branches.Select(b => b.ToResponse()).ToList(),
            x.Restriction?.ToResponse(),
            x.CreatedAt, x.UpdatedAt);

    public static List<DraweeBankResponse> ToResponseList(this IEnumerable<Finnova.Models.Domain.Entities.DraweeBank> items)
        => items.Select(i => i.ToResponse()).ToList();

    public static DraweeBankAuditEntryResponse ToResponse(this DraweeBankAuditEntry a) =>
        new(a.Id, a.DraweeBankId, a.Action.ToString(), a.BeforeSnapshot, a.AfterSnapshot,
            a.ChangedBy, a.ChangedAtUtc);
}
