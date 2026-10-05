using Finnova.Models.Contracts.DraweeBanks;

namespace Finnova.Tests.Infrastructure;

/// <summary>Fluent builder for valid India-only create/update requests used by example tests.</summary>
public sealed class DraweeBankBuilder
{
    private string _code = "HDFC";
    private string _name = "HDFC Bank Ltd";
    private bool? _isActive = null;
    private readonly List<DraweeBranchRequest> _branches = new();
    private RestrictionDetailRequest? _restriction;

    public DraweeBankBuilder WithCode(string code) { _code = code; return this; }
    public DraweeBankBuilder WithName(string name) { _name = name; return this; }
    public DraweeBankBuilder WithIsActive(bool? v) { _isActive = v; return this; }

    public DraweeBankBuilder WithBranch(string placeCode = "MUM", string placeName = "Mumbai",
        string address = "Fort", string pin = "400001", DateTime? start = null, DateTime? end = null)
    {
        _branches.Add(new DraweeBranchRequest(placeCode, placeName, address, pin,
            start ?? new DateTime(2024, 1, 1), end));
        return this;
    }

    public DraweeBankBuilder WithRestriction(int clearingDays = 2,
        DateTime? start = null, DateTime? end = null)
    {
        _restriction = new RestrictionDetailRequest(clearingDays,
            start ?? new DateTime(2024, 1, 1), end ?? new DateTime(2024, 12, 31));
        return this;
    }

    public CreateDraweeBankRequest BuildCreate()
        => new(_code, _name, _isActive, _branches, _restriction);

    public UpdateDraweeBankRequest BuildUpdate()
        => new(_name, _branches, _restriction);

    // Standalone challan-rule request for the dedicated rule route (rules are NOT part of the bank
    // aggregate; they are created via CreateChallanRuleCommand, not with the bank).
    public static CreateChallanRuleRequest BuildRule(string ruleCode = "R1", string format = "AA[0-9]{6}",
        string expr = "len(v)==8", string routing = "CLEARING-A", bool? active = null)
        => new(ruleCode, format, expr, routing, active);
}
