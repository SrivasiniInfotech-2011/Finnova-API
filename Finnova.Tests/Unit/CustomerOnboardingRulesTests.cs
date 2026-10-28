using System.Linq;
using FsCheck.Xunit;
using Xunit;
using Finnova.Models.Contracts.Customers;
using Finnova.Service.Customers.References;
using Finnova.Service.Customers.Validation;
using Finnova.Models.Domain.Exceptions;
public sealed class CustomerOnboardingRulesTests
{
    [Fact] public void CustomerCode_requires_twelve_alphanumeric_characters() => Assert.Throws<ArgumentException>(() => CustomerCodeRules.ValidateGenerated("short"));
    [Fact] public void GroupCode_rejects_more_than_four_digits() => Assert.ThrowsAny<Exception>(() => CustomerFieldRules.NumericCode("12345", "GroupCode", 4));
    [Fact] public void Dob_cannot_be_in_the_future() => Assert.ThrowsAny<Exception>(() => CustomerFieldRules.DateOfBirth(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1))));
    [Fact] public void Issue_must_precede_expiry() => Assert.ThrowsAny<Exception>(() => CustomerFieldRules.IssueBeforeExpiry(new DateOnly(2025, 2, 1), new DateOnly(2025, 1, 1)));
    [Fact] public void Bank_accounts_have_one_default() => Assert.ThrowsAny<Exception>(() => BankAccountRules.Validate(new[] { new BankAccountRequest(null, "1", "B", null, null, null, null, null, false) }));
    [Fact] public void Display_names_are_not_write_contract_fields() => Assert.DoesNotContain("GroupName", typeof(CustomerCreateRequest).GetProperties().Select(x => x.Name));

    // Property 1 validates Requirements 3.1 and 19.1: CustomerCode is exactly 12 alphanumeric characters.
    [Property(MaxTest = 100)]
    public bool Property1_CustomerCodeRule_is_stable(string value)
        => value is null || (value.Length == 12 && value.All(char.IsLetterOrDigit)) == !Throws(() => CustomerCodeRules.ValidateGenerated(value));

    // Property 2 validates Requirements 3.1 and 27.5: normalization is idempotent.
    [Property(MaxTest = 100)]
    public bool Property2_Normalized_identity_is_idempotent(string value)
        => value is null || DuplicateIdentityRules.Normalize(DuplicateIdentityRules.Normalize(value)) == DuplicateIdentityRules.Normalize(value);

    // Property 3 validates Requirements 21.1 and 27.5: amounts cannot be negative.
    [Property(MaxTest = 100)]
    public bool Property3_Negative_amounts_are_rejected(decimal value)
        => (value < 0) == Throws(() => CustomerFieldRules.NonNegative(value, "Amount"));

    // Property 4 validates Requirements 22.1 and 27.5: percentage values are bounded.
    [Property(MaxTest = 100)]
    public bool Property4_Percentage_is_bounded(decimal value)
        => (value is < 0 or > 100) == Throws(() => CustomerFieldRules.Percentage(value, "Stake"));

    // Property 5 validates Requirements 20.1 and 27.5: PIN values use the Indian six-digit rule.
    [Property(MaxTest = 100)]
    public bool Property5_Pin_is_six_digit_indian_pin(string value)
        => value is null || (value.Length != 6 || value[0] == '0' || value.Any(c => !char.IsDigit(c))) == Throws(() => CustomerFieldRules.IndianPin(value));

    // Property 6 validates Requirement 23.8: a non-empty bank set has exactly one default.
    [Property(MaxTest = 100)]
    public bool Property6_Default_account_count_is_one_when_present(bool first, bool second)
        => (first || second) && first != second ? !Throws(() => BankAccountRules.Validate(new[] { new BankAccountRequest(null, "1", "B", null, null, null, null, null, first), new BankAccountRequest(null, "2", "B", null, null, null, null, null, second) })) : true;

    // Property 7 validates Requirements 3.1 and 23.4: normalized identity is case-insensitive.
    [Property(MaxTest = 100)]
    public bool Property7_Code_normalization_is_case_insensitive(string value)
        => value is null || DuplicateIdentityRules.Normalize(value.ToLowerInvariant()) == DuplicateIdentityRules.Normalize(value.ToUpperInvariant());

    // Property 8 validates Requirements 21.8 and 27.5: issue date is strictly before expiry.
    [Property(MaxTest = 100)]
    public bool Property8_Date_rule_is_strict(int issueDay, int expiryDay)
    {
        var issue = DateOnly.FromDayNumber(Math.Abs(issueDay % 3_652_059));
        var expiry = DateOnly.FromDayNumber(Math.Abs(expiryDay % 3_652_059));
        return (issue >= expiry) == Throws(() => CustomerFieldRules.IssueBeforeExpiry(issue, expiry));
    }

    private static bool Throws(Action action) { try { action(); return false; } catch { return true; } }
}
