namespace Finnova.Models.Domain.ValueObjects;

public sealed record DemandPeriod(int Year, int Month)
{
    public static DemandPeriod Create(int year, int month)
    {
        if (year is < 2000 or > 2100) throw new ArgumentOutOfRangeException(nameof(year));
        if (month is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(month));
        return new DemandPeriod(year, month);
    }

    public string CanonicalValue => $"{Year:D4}-{Month:D2}";
}

public sealed record DemandScope(Guid LineOfBusinessId, IReadOnlyList<Guid> BranchIds)
{
    public static DemandScope Create(Guid lineOfBusinessId, IEnumerable<Guid> branchIds)
    {
        if (lineOfBusinessId == Guid.Empty) throw new ArgumentException("Line of Business is required.", nameof(lineOfBusinessId));
        var normalized = branchIds.Where(x => x != Guid.Empty).Distinct().OrderBy(x => x).ToArray();
        if (normalized.Length == 0) throw new ArgumentException("At least one Branch is required.", nameof(branchIds));
        return new DemandScope(lineOfBusinessId, normalized);
    }

    public string CanonicalValue => $"{LineOfBusinessId:N}:{string.Join(',', BranchIds.OrderBy(x => x).Select(x => x.ToString("N")))}";
}

public sealed record DemandLogicalKey(DemandScope Scope, string Frequency, DemandPeriod Period, string BillingReference)
{
    public string CanonicalValue => $"{Scope.CanonicalValue}|{Frequency.Trim()}|{Period.CanonicalValue}|{BillingReference.Trim()}";
}
