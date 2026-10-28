namespace Finnova.Models.Domain.Exceptions;

public static class CustomerErrorCodes
{
    public const string Family = "ERR-CUSTOMER";
    public const string Validation = Family + "-400";
    public const string NotFound = Family + "-404";
    public const string Conflict = Family + "-409";
    public const string Unexpected = Family + "-500";
    public const string BoundaryUnavailable = Family + "-503";
    public const string LookupReference = Family + "-LOOKUP-REF";
    public const string NationalityReference = Family + "-NATIONALITY-REF";
    public const string GLReference = Family + "-GL-REF";
    public const string DocumentReference = Family + "-DOCUMENT-REF";
    public const string AddressReference = Family + "-ADDRESS-REF";
    public const string Immutable = Family + "-IMMUTABLE";
    public const string DuplicateReference = Family + "-DUPLICATE-REF";
    public const string Duplicate = Family + "-DUPLICATE";
    public const string DefaultBank = Family + "-DEFAULT-BANK";
}

public abstract class CustomerDomainException : Exception
{
    protected CustomerDomainException(string message) : base(message) { }
    public string ErrorCode { get; protected init; } = CustomerErrorCodes.Validation;
}

public sealed class CustomerNotFoundException : CustomerDomainException
{
    public CustomerNotFoundException(string customerCode) : base("Customer was not found.") { ErrorCode = CustomerErrorCodes.NotFound; }
}
public sealed class CustomerCodeConflictException : CustomerDomainException
{
    public CustomerCodeConflictException() : base("Customer code is already in use.") { ErrorCode = CustomerErrorCodes.Conflict; }
}
public sealed class CustomerImmutableFieldException : CustomerDomainException
{
    public CustomerImmutableFieldException(string fieldName) : base($"The field '{fieldName}' cannot be changed in this mode.") { ErrorCode = CustomerErrorCodes.Immutable; }
}
public sealed class CustomerLookupReferenceNotFoundException : CustomerDomainException
{
    public CustomerLookupReferenceNotFoundException(string fieldName, string module, string lookupType, string code)
        : base($"The {fieldName} reference is not active in the configured Lookup Master scope.")
    {
        ErrorCode = CustomerErrorCodes.LookupReference;
        FieldName = fieldName; Module = module; LookupType = lookupType; Code = code;
    }
    public string FieldName { get; } public string Module { get; } public string LookupType { get; } public string Code { get; }
}
public sealed class CustomerNationalityReferenceNotFoundException : CustomerDomainException
{
    public CustomerNationalityReferenceNotFoundException(string code) : base("The nationality reference is not active.")
    {
        ErrorCode = CustomerErrorCodes.NationalityReference; Code = code;
    }
    public string Code { get; }
}
public sealed class CustomerGLPostingGroupInvalidException : CustomerDomainException
{
    public CustomerGLPostingGroupInvalidException(Guid companyId, string code, string reason)
        : base("The Customer posting group is not valid for the requested Company scope.")
    {
        ErrorCode = CustomerErrorCodes.GLReference;
        CompanyId = companyId; Code = code; Reason = reason;
    }
    public Guid CompanyId { get; } public string Code { get; } public string Reason { get; }
}
public sealed class CustomerDocumentDefinitionUnavailableException : CustomerDomainException
{
    public CustomerDocumentDefinitionUnavailableException(string constitutionCode, string entityRole)
        : base("No active applicable Constitution Document definition is available.")
    {
        ErrorCode = CustomerErrorCodes.DocumentReference;
        ConstitutionCode = constitutionCode; EntityRole = entityRole;
    }
    public string ConstitutionCode { get; } public string EntityRole { get; }
}
public sealed class CustomerAddressControlValidationException : CustomerDomainException
{
    public CustomerAddressControlValidationException(IReadOnlyCollection<string> errors)
        : base("The address was rejected by India Address Control.")
    {
        ErrorCode = CustomerErrorCodes.AddressReference; Errors = errors;
    }
    public IReadOnlyCollection<string> Errors { get; }
}
public sealed class CustomerConditionalApplicabilityException : CustomerDomainException
{
    public CustomerConditionalApplicabilityException(IReadOnlyCollection<string> errors)
        : base("One or more fields do not satisfy the Customer applicability rules.")
    {
        ErrorCode = CustomerErrorCodes.Validation; Errors = errors;
    }
    public IReadOnlyCollection<string> Errors { get; }
}
public sealed class CustomerDuplicateReferenceException : CustomerDomainException
{
    public CustomerDuplicateReferenceException(string referenceKind) : base("The supplied Customer reference is duplicated.")
    {
        ErrorCode = CustomerErrorCodes.DuplicateReference; ReferenceKind = referenceKind;
    }
    public string ReferenceKind { get; }
}
public sealed class CustomerDuplicateIdentityException : CustomerDomainException
{
    public CustomerDuplicateIdentityException() : base("The supplied identity conflicts with an existing Customer.") { ErrorCode = CustomerErrorCodes.Duplicate; }
}
public sealed class CustomerDuplicateBankAccountException : CustomerDomainException
{
    public CustomerDuplicateBankAccountException() : base("The bank account already exists for this Customer.") { ErrorCode = CustomerErrorCodes.Duplicate; }
}
public sealed class CustomerDefaultBankAccountException : CustomerDomainException
{
    public CustomerDefaultBankAccountException(string reason) : base($"The default bank-account rule was not satisfied: {reason}") { ErrorCode = CustomerErrorCodes.DefaultBank; }
}
public sealed class CustomerChildRecordNotFoundException : CustomerDomainException
{
    public CustomerChildRecordNotFoundException(string childType) : base($"The requested Customer {childType} record was not found.") { ErrorCode = CustomerErrorCodes.NotFound; }
}
public sealed class CustomerBoundaryUnavailableException : CustomerDomainException
{
    public CustomerBoundaryUnavailableException(string boundary) : base($"The {boundary} boundary is unavailable.") { ErrorCode = CustomerErrorCodes.BoundaryUnavailable; }
}
