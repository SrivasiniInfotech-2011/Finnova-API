using System.Text.Json;
using Finnova.Models.Domain.Entities;

namespace Finnova.Service.DraweeBank;

/// <summary>
/// Serialises the relevant state of a drawee bank aggregate (or a challan rule) to a compact JSON
/// projection stored in the audit before/after snapshots (R7.1, R7.2). One entry per
/// create/modify of the aggregate (design decision 8).
/// </summary>
public static class DraweeBankSnapshot
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    public static string Of(Finnova.Models.Domain.Entities.DraweeBank bank) => JsonSerializer.Serialize(new
    {
        bank.Id,
        bank.BankCode,
        bank.BankName,
        bank.IsActive,
        Branches = bank.Branches.Select(b => new
        {
            b.PlaceCode,
            b.PlaceName,
            b.Address,
            b.PostalCode,
            b.StartDate,
            b.EndDate
        }),
        Restriction = bank.Restriction == null ? null : new
        {
            bank.Restriction.ClearingDays,
            bank.Restriction.StartDate,
            bank.Restriction.EndDate
        }
    }, Options);

    public static string Of(ChallanRule rule) => JsonSerializer.Serialize(new
    {
        rule.Id,
        rule.DraweeBankId,
        rule.RuleCode,
        rule.FormatPattern,
        rule.ValidationExpression,
        rule.RoutingTarget,
        rule.IsActive
    }, Options);
}
