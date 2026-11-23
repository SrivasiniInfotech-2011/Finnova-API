using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Receipts;

public sealed record DenominationEntry(DenominationKind Kind, decimal FaceValue, string Label);

/// <summary>Indian denominations accepted on a Cash receipt (Assumption A1). Fixed, ordered list.</summary>
public static class CashDenominationCatalog
{
    public static readonly IReadOnlyList<DenominationEntry> Entries = new[]
    {
        new DenominationEntry(DenominationKind.Note, 500m, "Note 500"),
        new DenominationEntry(DenominationKind.Note, 200m, "Note 200"),
        new DenominationEntry(DenominationKind.Note, 100m, "Note 100"),
        new DenominationEntry(DenominationKind.Note, 50m, "Note 50"),
        new DenominationEntry(DenominationKind.Note, 20m, "Note 20"),
        new DenominationEntry(DenominationKind.Note, 10m, "Note 10"),
        new DenominationEntry(DenominationKind.Coin, 10m, "Coin 10"),
        new DenominationEntry(DenominationKind.Coin, 5m, "Coin 5"),
        new DenominationEntry(DenominationKind.Coin, 2m, "Coin 2"),
        new DenominationEntry(DenominationKind.Coin, 1m, "Coin 1"),
        new DenominationEntry(DenominationKind.Coin, 0.50m, "Coin 0.50 (50 paise)"),
    };

    public static bool Contains(DenominationKind kind, decimal faceValue)
        => Entries.Any(e => e.Kind == kind && e.FaceValue == faceValue);
}