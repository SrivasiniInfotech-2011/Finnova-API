using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Receipts;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace Finnova.Tests.Receipts;

public sealed class ReceiptHelperProperties
{
    // Feature: receipt-processing, Property 1: Doc Amount identity
    [Property(MaxTest = 200)]
    public bool DocAmount_equals_lines_plus_add_minus_less(NonEmptyArray<PositiveInt> lines, PositiveInt[] adds, PositiveInt[] lesses)
    {
        var l = lines.Get.Select(x => x.Get / 100m).ToList();
        var rows = adds.Select(x => (AddLessKind.Add, x.Get / 100m))
            .Concat(lesses.Select(x => (AddLessKind.Less, x.Get / 100m))).ToList();
        var expected = l.Sum() + adds.Sum(x => x.Get / 100m) - lesses.Sum(x => x.Get / 100m);
        var forward = ReceiptCalculator.DocAmount(l, rows);
        var reversed = ReceiptCalculator.DocAmount(Enumerable.Reverse(l), Enumerable.Reverse(rows));
        return forward == expected && reversed == expected;
    }

    // Feature: receipt-processing, Property 2: Adjustment inverse
    [Property(MaxTest = 200)]
    public bool Add_and_Less_of_the_same_value_cancel(NonEmptyArray<PositiveInt> lines, PositiveInt x)
    {
        var l = lines.Get.Select(v => v.Get / 100m).ToList();
        var amount = x.Get / 100m;
        var withPair = new[] { (AddLessKind.Add, amount), (AddLessKind.Less, amount) };
        return ReceiptCalculator.DocAmount(l, withPair) == ReceiptCalculator.LinesTotal(l);
    }

    // Feature: receipt-processing, Property 3: Denomination total
    [Property(MaxTest = 200)]
    public bool Denomination_total_is_the_sum_of_face_value_times_count(NonNegativeInt[] counts)
    {
        var entries = CashDenominationCatalog.Entries;
        var rows = counts.Take(entries.Count).Select((c, i) => (entries[i].FaceValue, c.Get % 1000)).ToList();
        var expected = rows.Sum(r => r.FaceValue * r.Item2);
        var perRow = rows.Sum(r => ReceiptCalculator.DenominationAmount(r.FaceValue, r.Item2));
        return ReceiptCalculator.DenominationTotal(rows) == expected && perRow == expected;
    }

    // Feature: receipt-processing, Property 5: Amount in words
    [Property(MaxTest = 200)]
    public bool Amount_in_words_has_the_required_shape(NonNegativeInt rupees, byte paiseRaw)
    {
        var paise = paiseRaw % 100;
        var amount = (rupees.Get % 999_999_999) + paise / 100m;
        var words = AmountInWords.ToWords(amount);
        return words.StartsWith("Rupees ", StringComparison.Ordinal)
            && words.EndsWith(" Only", StringComparison.Ordinal)
            && words.Contains("Paise", StringComparison.Ordinal) == (paise != 0)
            && words == AmountInWords.ToWords(amount);
    }

    [Theory]
    [InlineData("0", "Rupees Zero Only")]
    [InlineData("0.05", "Rupees Zero and Five Paise Only")]
    [InlineData("3500", "Rupees Three Thousand Five Hundred Only")]
    [InlineData("100000", "Rupees One Lakh Only")]
    [InlineData("10000000", "Rupees One Crore Only")]
    [InlineData("1234567.50", "Rupees Twelve Lakh Thirty-Four Thousand Five Hundred Sixty-Seven and Fifty Paise Only")]
    [InlineData("999999999.99", "Rupees Ninety-Nine Crore Ninety-Nine Lakh Ninety-Nine Thousand Nine Hundred Ninety-Nine and Ninety-Nine Paise Only")]
    public void Amount_in_words_known_vectors(string amount, string expected)
        => Assert.Equal(expected, AmountInWords.ToWords(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture)));

    [Theory]
    [InlineData("-0.01")]
    [InlineData("1000000000")]
    public void Amount_in_words_rejects_out_of_range(string amount)
        => Assert.Throws<ArgumentOutOfRangeException>(() =>
            AmountInWords.ToWords(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void Catalog_contains_the_indian_denominations_only()
    {
        Assert.True(CashDenominationCatalog.Contains(DenominationKind.Note, 500m));
        Assert.True(CashDenominationCatalog.Contains(DenominationKind.Coin, 0.50m));
        Assert.False(CashDenominationCatalog.Contains(DenominationKind.Note, 2000m));
        Assert.False(CashDenominationCatalog.Contains(DenominationKind.Coin, 500m));
    }
}