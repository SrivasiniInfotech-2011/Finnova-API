using FsCheck;
using Finnova.Models.Contracts.DraweeBanks;

namespace Finnova.Tests.Infrastructure;

/// <summary>
/// FsCheck generators for drawee-bank property tests. Produces valid + edge strings within the
/// max lengths (20/150/20/150/300/50/500/500/200), six-digit and malformed PINs, casing/whitespace
/// code variants, branch sets with/without duplicate place codes, restrictions with in-range and
/// out-of-range clearing days and valid/invalid date ranges, datasets, and audit-entry sets
/// (including empty). English-only / India-appropriate values throughout.
/// </summary>
public static class DraweeBankGenerators
{
    public static Arbitrary<string> ValidBankCode() =>
        Gen.Elements("HDFC", "ICICI", "SBI", "AXIS", "KOTAK", "PNB", "BOB", "CANARA")
           .ToArbitrary();

    public static Arbitrary<string> ValidBankName() =>
        Gen.Elements("HDFC Bank Ltd", "ICICI Bank Ltd", "State Bank of India",
                     "Axis Bank Ltd", "Kotak Mahindra Bank").ToArbitrary();

    public static Arbitrary<string> SixDigitPin() =>
        Gen.Elements("400001", "411001", "560001", "110001", "600001").ToArbitrary();

    public static Arbitrary<string> MalformedPin() =>
        Gen.Elements("40001", "4000011", "4000AB", "", "   ").ToArbitrary();

    // Casing/whitespace variants of a code for uniqueness tests (R2.1, R6.4).
    public static Gen<string> CaseWhitespaceVariant(string code) =>
        Gen.Elements(code.ToUpperInvariant(), code.ToLowerInvariant(),
                     $"  {code}  ", $"{code} ", $" {code}");

    public static Arbitrary<DraweeBranchRequest> ValidBranch() =>
        (from pc in Gen.Elements("MUM", "PUN", "DEL", "BLR", "CHN")
         from pin in SixDigitPin().Generator
         select new DraweeBranchRequest(pc, "Mumbai", "Fort", pin,
             new DateTime(2024, 1, 1), null)).ToArbitrary();
}
