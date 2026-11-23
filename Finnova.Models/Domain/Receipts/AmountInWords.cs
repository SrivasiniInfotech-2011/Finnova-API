using System.Text;

namespace Finnova.Models.Domain.Receipts;

/// <summary>
/// INR amount to words using the Indian numbering system (R10). Range 0.00 to 99,99,99,999.99.
/// Example: 3500.00 -> "Rupees Three Thousand Five Hundred Only".
/// </summary>
public static class AmountInWords
{
    private static readonly string[] Ones =
    {
        "Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten",
        "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen"
    };

    private static readonly string[] Tens =
    {
        "", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety"
    };

    public static string ToWords(decimal amount)
    {
        if (amount < 0m || amount > ReceiptCalculator.MaxAmount)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be between 0.00 and 99,99,99,999.99.");

        var rounded = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        var rupees = (long)decimal.Truncate(rounded);
        var paise = (int)decimal.Round((rounded - rupees) * 100m, 0, MidpointRounding.AwayFromZero);

        var sb = new StringBuilder("Rupees ");
        sb.Append(rupees == 0 ? "Zero" : Words(rupees));
        if (paise > 0) sb.Append(" and ").Append(Words(paise)).Append(" Paise");
        sb.Append(" Only");
        return sb.ToString();
    }

    private static string Words(long n)
    {
        var parts = new List<string>();
        var crore = (int)(n / 10_000_000); n %= 10_000_000;
        var lakh = (int)(n / 100_000); n %= 100_000;
        var thousand = (int)(n / 1_000); n %= 1_000;
        var hundred = (int)(n / 100);
        var rest = (int)(n % 100);

        if (crore > 0) parts.Add(Below100(crore) + " Crore");
        if (lakh > 0) parts.Add(Below100(lakh) + " Lakh");
        if (thousand > 0) parts.Add(Below100(thousand) + " Thousand");
        if (hundred > 0) parts.Add(Ones[hundred] + " Hundred");
        if (rest > 0) parts.Add(Below100(rest));
        return string.Join(' ', parts);
    }

    private static string Below100(int n)
        => n < 20 ? Ones[n] : Tens[n / 10] + (n % 10 == 0 ? string.Empty : "-" + Ones[n % 10]);
}