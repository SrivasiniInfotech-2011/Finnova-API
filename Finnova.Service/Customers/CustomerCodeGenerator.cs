using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Numbering;
using Finnova.Repository.Interfaces;

namespace Finnova.Service.Customers;

public sealed class CustomerCodeGenerator(INumberingSchemeRepository numberingSchemes) : IDocumentNumberGenerator
{
    public async Task<string> GenerateAsync(string scope, int length, CancellationToken ct = default)
    {
        if (!string.Equals(scope, "CUSTOMER", StringComparison.Ordinal))
            throw new ArgumentException("Customer codes require the CUSTOMER document type.", nameof(scope));

        var issued = await numberingSchemes.IssueNextAsync(
            "CUSTOMER", NumberScope.Global, null, DateTime.UtcNow, ct)
            ?? throw new InvalidOperationException("The CUSTOMER numbering scheme is not configured.");
        var (scheme, sequence) = issued;
        var code = NumberFormatter.Format(scheme.FormatTemplate, sequence, scheme.SeqPadding,
            scheme.Prefix, scheme.Suffix, DateTime.UtcNow);
        if (code.Length != length || !code.All(char.IsLetterOrDigit))
            throw new InvalidOperationException("The CUSTOMER numbering scheme did not produce a valid customer code.");
        return code;
    }
}
