using System.Globalization;
using System.Net.Mail;
using System.Text.RegularExpressions;
using Finnova.Models.Contracts.Customers;
using Finnova.Models.Domain.Exceptions;
using Finnova.Service.Customers.References;
namespace Finnova.Service.Customers.Validation;
public sealed class CustomerFieldApplicabilityEvaluator
{
    public void Validate(CustomerApplicabilityContext context)
    {
        if (string.IsNullOrWhiteSpace(context.CustomerTypeCode)) throw new CustomerConditionalApplicabilityException(new[] { "CustomerTypeCode is required." });
        if (string.Equals(context.Mode, "Query", StringComparison.OrdinalIgnoreCase)) return;
        // Product-approved matrix values are inserted only at this bounded seam.
        var unavailable = context.BusinessSubTypeCode is not null && context.BusinessTypeCode is null;
        if (unavailable) throw new CustomerConditionalApplicabilityException(new[] { "CustomerBusinessSubTypeCode requires CustomerBusinessTypeCode." });
    }
}
public static class CustomerCodeRules
{
    public static void ValidateGenerated(string code) { if (code.Length != 12 || !code.All(char.IsLetterOrDigit)) throw new ArgumentException("CustomerCode must be exactly 12 alphanumeric characters."); }
    public static void EnsureImmutable(string current, string requested) { if (!string.Equals(current, requested, StringComparison.Ordinal)) throw new CustomerImmutableFieldException(nameof(CustomerResponse.CustomerCode)); }
    public static void RejectDelete() => throw new CustomerImmutableFieldException("CustomerCode");
}
public static class CustomerFieldRules
{
    public static void NumericCode(string? value, string field, int maxDigits) { if (value is not null && !Regex.IsMatch(value, $"^\\d{{1,{maxDigits}}}$")) throw new CustomerConditionalApplicabilityException(new[] { $"{field} must contain no more than {maxDigits} numeric digits." }); }
    public static void Length(string? value, string field, int max) { if (value is not null && value.Length > max) throw new CustomerConditionalApplicabilityException(new[] { $"{field} exceeds its maximum length." }); }
    public static void NonNegative(decimal? value, string field) { if (value < 0) throw new CustomerConditionalApplicabilityException(new[] { $"{field} cannot be negative." }); }
    public static void Percentage(decimal? value, string field) { if (value is < 0 or > 100) throw new CustomerConditionalApplicabilityException(new[] { $"{field} must be between 0 and 100." }); }
    public static void DateOfBirth(DateOnly? value) { if (value > DateOnly.FromDateTime(DateTime.UtcNow)) throw new CustomerConditionalApplicabilityException(new[] { "DateOfBirth cannot be in the future." }); }
    public static void IssueBeforeExpiry(DateOnly? issue, DateOnly? expiry) { if (issue.HasValue && expiry.HasValue && issue >= expiry) throw new CustomerConditionalApplicabilityException(new[] { "Issue date must be before expiry date." }); }
    public static void Email(string? value) { if (string.IsNullOrWhiteSpace(value)) return; try { _ = new MailAddress(value); } catch { throw new CustomerConditionalApplicabilityException(new[] { "Email is not valid." }); } }
    public static void Website(string? value) { if (string.IsNullOrWhiteSpace(value)) return; if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) throw new CustomerConditionalApplicabilityException(new[] { "Website is not valid." }); }
    public static void IndianPin(string? value) { if (value is not null && !Regex.IsMatch(value, "^[1-9][0-9]{5}$")) throw new CustomerConditionalApplicabilityException(new[] { "PostalCode must be a six-digit Indian PIN." }); }
    public static void DateDdMMyyyy(string? value, string field) { if (value is not null && !DateOnly.TryParseExact(value, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) throw new CustomerConditionalApplicabilityException(new[] { $"{field} must use DD/MM/YYYY." }); }
}
public static class BankAccountRules
{
    public static string Normalize(Guid customerId, string accountNumber, string? bankCode) => $"{customerId:N}|{bankCode?.Trim().ToUpperInvariant()}|{accountNumber.Trim().ToUpperInvariant()}";
    public static void Validate(IReadOnlyCollection<BankAccountRequest> accounts)
    {
        var duplicate = accounts.GroupBy(x => (x.BankCode?.Trim().ToUpperInvariant(), x.AccountNumber.Trim().ToUpperInvariant())).Any(g => g.Count() > 1);
        if (duplicate) throw new CustomerDuplicateBankAccountException();
        if (accounts.Count > 0 && accounts.Count(x => x.IsDefault) != 1) throw new CustomerDefaultBankAccountException("exactly one default account is required when accounts exist");
    }
}
public static class DocumentRules
{
    public static void RejectRawBinary(object? value) { if (value is byte[]) throw new CustomerConditionalApplicabilityException(new[] { "Raw document binary is not accepted; provide metadata and a DMS reference." }); }
    public static void ValidateApplicable(Guid requested, IReadOnlyCollection<DocumentDefinitionProjection> definitions) { if (!definitions.Any(x => x.Id == requested)) throw new CustomerDocumentDefinitionUnavailableException("<request-constitution>", "<request-entity-role>"); }
}
public static class DuplicateIdentityRules
{
    public static string Normalize(string value) => Regex.Replace(value.Trim().ToUpperInvariant(), "\\s+", " ");
    public static void RejectIfExisting(bool exists) { if (exists) throw new CustomerDuplicateIdentityException(); }
}
public static class CustomerPrerequisiteRules
{
    public static void RequireModifyMode(string mode) { if (!string.Equals(mode, "Modify", StringComparison.OrdinalIgnoreCase)) throw new CustomerConditionalApplicabilityException(new[] { "The operation is available only in Modify mode." }); }
    public static void RequireTrackBoundary(bool available) { if (!available) throw new CustomerBoundaryUnavailableException("Account/Contract"); }
    public static void RequireFactoring(bool applicable, string? type) { if (applicable && string.IsNullOrWhiteSpace(type)) throw new CustomerConditionalApplicabilityException(new[] { "FactoringTypeCode is required when factoring applies." }); }
    public static void RequireCreditValidUpToOnly(bool changedOtherField) { if (changedOtherField) throw new CustomerImmutableFieldException("CustomerCreditDetails"); }
}
