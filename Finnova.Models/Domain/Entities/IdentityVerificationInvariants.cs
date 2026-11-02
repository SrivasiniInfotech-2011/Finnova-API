using Finnova.Models.Domain.Enums;
namespace Finnova.Models.Domain.Entities;
public static class IdentityVerificationInvariants
{
    public static void EnsureResultDomain(ValidationRecord record)
    {
        var identifier = record.OperationType is VerificationOperationType.PanIdentifierValidation or VerificationOperationType.AadhaarIdentifierValidation;
        var comparison = record.OperationType is VerificationOperationType.PanNameVerification or VerificationOperationType.AadhaarAddressVerification;
        if (identifier == comparison) throw new InvalidOperationException("Identity verification operation type is invalid.");
        if (identifier && (record.ValidationStatus is null || record.ComparisonOutcome is not null)) throw new InvalidOperationException("Identifier records require validation status only.");
        if (comparison && (record.ComparisonOutcome is null || record.ValidationStatus is not null)) throw new InvalidOperationException("Comparison records require comparison outcome only.");
    }
}
