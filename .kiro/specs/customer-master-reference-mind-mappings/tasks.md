# Implementation Plan: Customer Onboarding (Current Implementation Slice)

## Overview

This plan implements Customer Onboarding only across the existing Finnova backend and frontend repositories. It persists the Customer aggregate and approved children, validates every controlled reference through the existing master-data boundaries, projects current display values on reads, enforces the FRS validation/applicability rules, and exposes authorized Create/Modify/Query flows.

- **Backend:** `e:\Finnova\Finnova-API`, C#/.NET, `Finnova.SystemAdminService`, MediatR, FluentValidation, EF Core, `FinnovaDbContext`, and the existing repository/DI conventions.
- **Frontend:** `E:\Finnova\Finnova-UI\Finnova-UI`, React 18, TypeScript, MUI, shared `src/services/api.ts`, feature interface/real/mock/toggle services, and Vitest/React Testing Library.
- **Authoritative boundaries:** `LookupValue` through `ILookupRepository.GetActiveByModuleAndTypeAsync`, `Nationality` through `INationalityRepository`, `GLAccount` through `IGLAccountRepository`, `Document`/`DocumentApplicabilityRule` through `IDocumentRepository.GetActiveForConsumptionAsync`, and an explicit `IIndiaAddressControl` adapter seam.
- **Persistence rule:** Customer stores reference codes/IDs and approved address values/references. It never stores authoritative `GroupName`, `IndustryName`, `ConstitutionName`, `NationalityName`, or `GLName` fields. Those values are query-time projections only.
- **Language/localization:** India-only, English-only, left-to-right UI. Do not add bilingual fields, translation scaffolding, Arabic/non-English seed data, RTL behavior, Redux, or RxJS.
- **User execution:** Every code block is intended to be copied into the named file or applied at the named insertion point. `<RESOLVE:...>` markers are bounded contract seams, not permission to invent values. The user runs migration/build/test commands manually after applying the tasks.

## Tasks

- [x] 0. Scope and source contract gate
  - [x] 0.1 Confirm the Customer FRS contract and the only unresolved seams before copying implementation code.
    - Treat Requirements 18–27 and the supplied Customer FRS as the source for Customer fields, modes, sections, child records, limits, and applicability behavior.
    - Keep these as the only current-slice contract seams: final Lookup module/type constants, Nationality active-code behavior, GL Company scope and approved/postable rule, Document entity-role mapping, and the explicit India Address Control interface.
    - The Customer aggregate stores `GroupCode` (maximum four numeric digits), `IndustryCode` (maximum three characters), `ConstitutionCode`, `TitleCode`, `MaritalStatusCode`, `CustomerBusinessTypeCode`, `CustomerBusinessSubTypeCode`, `IndustrySectorCode`, `SubSectorCode`, `NationalityCode`, `CustomerPostingGroupCode`, and optionally the approved `GLAccountId`. It stores no authoritative master display names.
    - `CustomerTrackEntry.TrackTypeCode` is a Lookup code. Constitution documents are resolved from active applicable Document Master definitions and Customer stores metadata/DMS references only.
    - Company scope is an explicit request/context input or an approved resolver boundary. Never infer CompanyId from a Customer code, GL name, or an unrelated record.
    - Customer Reference persistence/management is deferred. MassTransit, broker, outbox, publisher, consumer, worker, and asynchronous Mind Mapping generation are deferred. PayU/Python KYC, behavior-model training, and future AI inference are deferred. None is a prerequisite for Tasks 1–12.
    - Do not seed final Customer Lookup values. Existing `Origination/MARITAL_STATUS` may be referenced only when the deployed Lookup Master already contains it; do not create or invent values.
    - **Requirements:** 1.1–4.6, 10.1–13.8, 18.1–27.10 and the authoritative-boundary assumptions in `requirements.md`.

- [x] 1. Create Customer domain entities and lifecycle/audit enums
  - [x] 1.1 CREATE `Finnova.Models/Domain/Entities/Customer.cs`, `CustomerAddress.cs`, `IndividualCustomerDetails.cs`, `NonIndividualCustomerDetails.cs`, `BankAccount.cs`, `CustomerDocumentReference.cs`, `CustomerTrackEntry.cs`, `CustomerCreditDetails.cs`, `FactoringSubLimit.cs`, and `CustomerAuditEntry.cs`.
    - Add the following complete files. The entity model uses only Customer-owned values and master references. No `GroupName`, `IndustryName`, `ConstitutionName`, `NationalityName`, or `GLName` property is allowed on a persisted entity.
    ```csharp
    // CREATE Finnova.Models/Domain/Entities/Customer.cs
    using Finnova.Models.Domain.Enums;
    namespace Finnova.Models.Domain.Entities;

    public sealed class Customer
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public string CustomerCode { get; private set; } = string.Empty; // generated, exactly 12, immutable
        public Guid CompanyId { get; private set; } // explicit Company scope for GL validation
        public bool IsCustomer { get; private set; }
        public bool IsGuarantor { get; private set; }
        public bool IsCoApplicant { get; private set; }
        public string CustomerTypeCode { get; private set; } = string.Empty;
        public string? GroupCode { get; private set; } // max 4 numeric digits
        public string? IndustryCode { get; private set; } // max 3 characters
        public string? CustomerPostingGroupCode { get; private set; }
        public Guid? GLAccountId { get; private set; }
        public string? ConstitutionCode { get; private set; }
        public string? NationalityCode { get; private set; }
        public string? TitleCode { get; private set; }
        public string? MaritalStatusCode { get; private set; }
        public string? CustomerBusinessTypeCode { get; private set; }
        public string? CustomerBusinessSubTypeCode { get; private set; }
        public string? IndustrySectorCode { get; private set; }
        public string? SubSectorCode { get; private set; }
        public string? FirstName { get; private set; }
        public string? SecondName { get; private set; }
        public string? ThirdName { get; private set; }
        public string? FourthName { get; private set; }
        public string? FifthName { get; private set; }
        public string? SixthName { get; private set; }
        public string? Notes { get; private set; }
        public bool? IsVip { get; private set; }
        public bool? IsSeniorMember { get; private set; }
        public string? FinancialRequirement { get; private set; }
        public string? Accommodation { get; private set; }
        public bool? IsRelatedParty { get; private set; }
        public string? RelationCode { get; private set; }
        public string? EmployerEconomicActivityCode { get; private set; }
        public CustomerLifecycleStatus Status { get; private set; } = CustomerLifecycleStatus.Active;
        public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; private set; } = DateTime.UtcNow;

        public List<CustomerAddress> Addresses { get; private set; } = new();
        public IndividualCustomerDetails? IndividualDetails { get; private set; }
        public NonIndividualCustomerDetails? NonIndividualDetails { get; private set; }
        public List<BankAccount> BankAccounts { get; private set; } = new();
        public List<CustomerDocumentReference> Documents { get; private set; } = new();
        public List<CustomerTrackEntry> Tracks { get; private set; } = new();
        public List<FactoringSubLimit> FactoringSubLimits { get; private set; } = new();

        public void SetGeneratedCode(string code) => CustomerCode = code;
        public void Touch() => UpdatedAtUtc = DateTime.UtcNow;
    }

    // CREATE Finnova.Models/Domain/Entities/CustomerAddress.cs
    public sealed class CustomerAddress
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid CustomerId { get; private set; }
        public CustomerAddressType AddressType { get; private set; }
        public string? HouseNumber { get; private set; }
        public string? BlockNumber { get; private set; }
        public string? PostalCode { get; private set; }
        public string? City { get; private set; }
        public string? State { get; private set; }
        public string? Country { get; private set; }
        public string? POBoxNumber { get; private set; }
        public string? WayNumber { get; private set; }
        public string? Telephone { get; private set; }
        public string? ResidenceFaxNumber { get; private set; }
        public string? Mobile { get; private set; }
        public string? Email { get; private set; }
        public string? Website { get; private set; }
        public string? ContactPerson { get; private set; }
        public string? ContactPersonPhoneNumber { get; private set; }
        public string? ContactPersonFlatNumber { get; private set; }
        public string? AreaSheik { get; private set; }
        public string? LocationLandmark { get; private set; }
        public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; private set; } = DateTime.UtcNow;
    }

    // CREATE Finnova.Models/Domain/Entities/IndividualCustomerDetails.cs
    public sealed class IndividualCustomerDetails
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid CustomerId { get; private set; }
        public string? GenderCode { get; private set; }
        public DateOnly? DateOfBirth { get; private set; }
        public string? SpouseName { get; private set; }
        public int? Children { get; private set; }
        public int? TotalDependents { get; private set; }
        public DateOnly? WeddingAnniversaryDate { get; private set; }
        public string? HouseOrFlat { get; private set; }
        public bool? Own { get; private set; }
        public decimal? CurrentMarketValue { get; private set; }
        public decimal? RemainingLoanValue { get; private set; }
        public decimal? TotalNetWorth { get; private set; }
        public string? Qualification { get; private set; }
        public string? Profession { get; private set; }
        public string? IndiaIdentityType { get; private set; }
        public string? IndiaIdentityReference { get; private set; }
        public DateOnly? IdentityIssueDate { get; private set; }
        public DateOnly? IdentityExpiryDate { get; private set; }
        public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; private set; } = DateTime.UtcNow;
    }

    // CREATE Finnova.Models/Domain/Entities/NonIndividualCustomerDetails.cs
    public sealed class NonIndividualCustomerDetails
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid CustomerId { get; private set; }
        public string? PublicOrCloselyHeldCode { get; private set; }
        public int? NumberOfDirectorsOrPartners { get; private set; }
        public string? KeyDecisionMaker { get; private set; }
        public bool? ListedAtStockExchange { get; private set; }
        public decimal? PaidUpCapital { get; private set; }
        public decimal? FaceValueOfShares { get; private set; }
        public decimal? BookValueOfShares { get; private set; }
        public string? BusinessProfile { get; private set; }
        public decimal? MonthlyBusinessIncome { get; private set; }
        public string? GeographicalCoverage { get; private set; }
        public int? NumberOfBranches { get; private set; }
        public bool? GovernmentInstitutionalParticipation { get; private set; }
        public decimal? PromoterStakePercentage { get; private set; }
        public string? JVPartnerName { get; private set; }
        public decimal? JVPartnerStake { get; private set; }
        public string? CEOOrMDName { get; private set; }
        public int? AgeOfCEOOrMD { get; private set; }
        public int? ExperienceInYears { get; private set; }
        public string? ResidentialAddress { get; private set; }
        public DateOnly? WeddingAnniversaryDate { get; private set; }
        public string? CustomerBusinessTypeCode { get; private set; }
        public string? CustomerBusinessSubTypeCode { get; private set; }
        public string? IndustrySectorCode { get; private set; }
        public string? SubSectorCode { get; private set; }
        public string? CRNumber { get; private set; }
        public DateOnly? CRIssueDate { get; private set; }
        public DateOnly? CRExpiryDate { get; private set; }
        public string? BusinessActivities { get; private set; }
        public bool? FactoringApplicable { get; private set; }
        public string? FactoringTypeCode { get; private set; }
        public decimal? FactoringLimit { get; private set; }
        public string? Covenants { get; private set; }
        public string? CovenantClause { get; private set; }
        public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; private set; } = DateTime.UtcNow;
    }

    // CREATE Finnova.Models/Domain/Entities/BankAccount.cs
    public sealed class BankAccount
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid CustomerId { get; private set; }
        public string? AccountTypeCode { get; private set; }
        public string AccountNumber { get; private set; } = string.Empty;
        public string? BankCode { get; private set; }
        public string? MICRCode { get; private set; }
        public DateOnly? ClearingDate { get; private set; }
        public string? Address { get; private set; }
        public string? ContractNumber { get; private set; }
        public string? SubContractNumber { get; private set; }
        public string NormalizedDuplicateKey { get; private set; } = string.Empty;
        public bool IsDefault { get; private set; }
        public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; private set; } = DateTime.UtcNow;
    }

    // CREATE Finnova.Models/Domain/Entities/CustomerDocumentReference.cs
    public sealed class CustomerDocumentReference
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid CustomerId { get; private set; }
        public string ConstitutionCode { get; private set; } = string.Empty;
        public Guid DocumentDefinitionId { get; private set; }
        public string DocumentCode { get; private set; } = string.Empty;
        public string? DocumentCategory { get; private set; }
        public string? DocumentDescription { get; private set; }
        public bool IsMandatory { get; private set; }
        public bool? IsImageRequired { get; private set; }
        public string? Remarks { get; private set; }
        public bool IsCollected { get; private set; }
        public bool IsScanned { get; private set; }
        public string? Value { get; private set; }
        public string? ValueSource { get; private set; }
        public string? DmsReference { get; private set; }
        public string? ViewMetadata { get; private set; }
        public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; private set; } = DateTime.UtcNow;
    }

    // CREATE Finnova.Models/Domain/Entities/CustomerTrackEntry.cs
    public sealed class CustomerTrackEntry
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid CustomerId { get; private set; }
        public string? LineOfBusinessCode { get; private set; }
        public string? AccountNumber { get; private set; }
        public string TrackTypeCode { get; private set; } = string.Empty;
        public DateOnly TrackDate { get; private set; }
        public string? Reason { get; private set; }
        public string? ByLogin { get; private set; }
        public DateOnly? ReleaseDate { get; private set; }
        public string? ReleasedBy { get; private set; }
        public CustomerTrackStatus Status { get; private set; } = CustomerTrackStatus.Blacklisted;
        public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; private set; } = DateTime.UtcNow;
    }

    // CREATE Finnova.Models/Domain/Entities/CustomerCreditDetails.cs
    public sealed record CustomerCreditDetails
    {
        public Guid CustomerId { get; init; }
        public string? LineOfBusiness { get; init; }
        public string? ProductGroup { get; init; }
        public decimal SanctionedAmount { get; init; }
        public decimal ApprovedAmount { get; init; }
        public decimal UtilizedAmount { get; init; }
        public decimal RemainingAmount { get; init; }
        public DateOnly? ValidUpTo { get; init; }
        public string? FacilityType { get; init; }
    }

    // CREATE Finnova.Models/Domain/Entities/FactoringSubLimit.cs
    public sealed class FactoringSubLimit
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid CustomerId { get; private set; }
        public bool FactoringApplicable { get; private set; }
        public string? FactoringTypeCode { get; private set; }
        public decimal? Limit { get; private set; }
        public string? Covenant { get; private set; }
        public string? CovenantClause { get; private set; }
        public DateOnly? CutOffDate { get; private set; }
        public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; private set; } = DateTime.UtcNow;
    }

    // CREATE Finnova.Models/Domain/Entities/CustomerAuditEntry.cs
    public sealed class CustomerAuditEntry
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid CustomerId { get; private set; }
        public CustomerAuditAction Action { get; private set; }
        public string ActorId { get; private set; } = string.Empty;
        public string ChangedFieldsJson { get; private set; } = "{}";
        public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    }
    ```
    - The private setters are populated by the aggregate factory/update methods added in Task 7 or by the existing project convention. Do not add raw document bytes, verifier payloads, secrets, copied master names, or a local Lookup/Nationality/GL/Document/Address master.
    - **Requirements:** 2.1–4.6, 10.1–12.20, 18.1–18.10, 19.1–19.10, 20.1–20.8, 21.1–21.11, 22.1–22.8, 23.1–23.10, 24.1–24.9, 25.1–25.8, 26.1–26.9, 27.1–27.4.

  - [x] 1.2 CREATE `Finnova.Models/Domain/Enums/CustomerLifecycleStatus.cs`, `CustomerAddressType.cs`, `CustomerTrackStatus.cs`, and `CustomerAuditAction.cs`.
    ```csharp
    // CREATE Finnova.Models/Domain/Enums/CustomerLifecycleStatus.cs
    namespace Finnova.Models.Domain.Enums;
    public enum CustomerLifecycleStatus { Active = 1, Inactive = 2 }

    // CREATE Finnova.Models/Domain/Enums/CustomerAddressType.cs
    namespace Finnova.Models.Domain.Enums;
    public enum CustomerAddressType { Communication = 1, Permanent = 2 }

    // CREATE Finnova.Models/Domain/Enums/CustomerTrackStatus.cs
    namespace Finnova.Models.Domain.Enums;
    public enum CustomerTrackStatus { Blacklisted = 1, Released = 2 }

    // CREATE Finnova.Models/Domain/Enums/CustomerAuditAction.cs
    namespace Finnova.Models.Domain.Enums;
    public enum CustomerAuditAction { Created = 1, Modified = 2, BankChanged = 3, DocumentChanged = 4, TrackChanged = 5, CreditChanged = 6, FactoringChanged = 7 }
    ```
    - Do not create enums for Customer Type, Group, Industry, Constitution, Title, Marital Status, Business Type/Sub-Type, Sector/Sub-Sector, Track Type, Nationality, GL, or Document values. Those are string codes resolved through master boundaries.
    - **Requirements:** 18.1, 18.8, 27.1–27.4.

- [x] 2. Create Customer exceptions and safe error-code family
  - [x] 2.1 CREATE `Finnova.Models/Domain/Exceptions/CustomerExceptions.cs`.
    ```csharp
    // CREATE Finnova.Models/Domain/Exceptions/CustomerExceptions.cs
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
    ```

    - Exception messages must remain safe. Do not include raw PII, document contents, SQL, secrets, access tokens, or external payloads. Keep field/module/type/code/company data as structured properties for logging/problem-details mapping, not as raw payloads.
    - **Requirements:** 18.1–18.10, 19.1–19.10, 20.1–20.8, 21.1–21.11, 22.1–22.8, 23.1–23.10, 24.1–24.9, 25.1–25.8, 26.1–26.9, 27.6–27.10.

- [x] 3. Create Customer contracts
  - [x] 3.1 CREATE `Finnova.Models/Contracts/Customers/CustomerContracts.cs`.
    ```csharp
    // CREATE Finnova.Models/Contracts/Customers/CustomerContracts.cs
    using Finnova.Models.Domain.Enums;

    namespace Finnova.Models.Contracts.Customers;

    public sealed record CustomerAddressRequest(
        CustomerAddressType AddressType, string? HouseNumber, string? BlockNumber, string? PostalCode,
        string? City, string? State, string? Country, string? POBoxNumber, string? WayNumber,
        string? Telephone, string? ResidenceFaxNumber, string? Mobile, string? Email, string? Website,
        string? ContactPerson, string? ContactPersonPhoneNumber, string? ContactPersonFlatNumber,
        string? AreaSheik, string? LocationLandmark);

    public sealed record IndividualCustomerDetailsRequest(
        string? GenderCode, DateOnly? DateOfBirth, string? SpouseName, int? Children, int? TotalDependents,
        DateOnly? WeddingAnniversaryDate, string? HouseOrFlat, bool? Own, decimal? CurrentMarketValue,
        decimal? RemainingLoanValue, decimal? TotalNetWorth, string? Qualification, string? Profession,
        string? IndiaIdentityType, string? IndiaIdentityReference, DateOnly? IdentityIssueDate,
        DateOnly? IdentityExpiryDate);

    public sealed record NonIndividualCustomerDetailsRequest(
        string? PublicOrCloselyHeldCode, int? NumberOfDirectorsOrPartners, string? KeyDecisionMaker,
        bool? ListedAtStockExchange, decimal? PaidUpCapital, decimal? FaceValueOfShares,
        decimal? BookValueOfShares, string? BusinessProfile, decimal? MonthlyBusinessIncome,
        string? GeographicalCoverage, int? NumberOfBranches, bool? GovernmentInstitutionalParticipation,
        decimal? PromoterStakePercentage, string? JVPartnerName, decimal? JVPartnerStake,
        string? CEOOrMDName, int? AgeOfCEOOrMD, int? ExperienceInYears, string? ResidentialAddress,
        DateOnly? WeddingAnniversaryDate, string? CustomerBusinessTypeCode, string? CustomerBusinessSubTypeCode,
        string? IndustrySectorCode, string? SubSectorCode, string? CRNumber, DateOnly? CRIssueDate,
        DateOnly? CRExpiryDate, string? BusinessActivities, bool? FactoringApplicable,
        string? FactoringTypeCode, decimal? FactoringLimit, string? Covenants, string? CovenantClause);

    public sealed record BankAccountRequest(
        string? AccountTypeCode, string AccountNumber, string? BankCode, string? MICRCode,
        DateOnly? ClearingDate, string? Address, string? ContractNumber, string? SubContractNumber,
        bool IsDefault);

    public sealed record CustomerDocumentReferenceRequest(
        string ConstitutionCode, Guid DocumentDefinitionId, string DocumentCode, string? Remarks,
        bool IsCollected, bool IsScanned, string? Value, string? ValueSource, string? DmsReference,
        string? ViewMetadata);

    public sealed record CustomerTrackRequest(
        string? LineOfBusinessCode, string? AccountNumber, string TrackTypeCode, DateOnly TrackDate,
        string? Reason, string? ByLogin);

    public sealed record FactoringSubLimitRequest(
        bool FactoringApplicable, string? FactoringTypeCode, decimal? Limit, string? Covenant,
        string? CovenantClause, DateOnly? CutOffDate);

    public sealed record CustomerCreateRequest(
        Guid CompanyId, bool IsCustomer, bool IsGuarantor, bool IsCoApplicant, string CustomerTypeCode,
        string? GroupCode, string? IndustryCode, string? CustomerPostingGroupCode, Guid? GLAccountId,
        string? ConstitutionCode, string? NationalityCode, string? TitleCode, string? MaritalStatusCode,
        string? CustomerBusinessTypeCode, string? CustomerBusinessSubTypeCode, string? IndustrySectorCode,
        string? SubSectorCode, string? FirstName, string? SecondName, string? ThirdName, string? FourthName,
        string? FifthName, string? SixthName, string? Notes, bool? IsVip, bool? IsSeniorMember,
        string? FinancialRequirement, string? Accommodation, bool? IsRelatedParty, string? RelationCode,
        string? EmployerEconomicActivityCode, IReadOnlyCollection<CustomerAddressRequest> Addresses,
        IndividualCustomerDetailsRequest? IndividualDetails, NonIndividualCustomerDetailsRequest? NonIndividualDetails,
        IReadOnlyCollection<BankAccountRequest> BankAccounts, IReadOnlyCollection<CustomerDocumentReferenceRequest> Documents,
        IReadOnlyCollection<CustomerTrackRequest> Tracks, IReadOnlyCollection<FactoringSubLimitRequest> FactoringSubLimits);

    public sealed record CustomerModifyRequest(
        string CustomerCode, Guid CompanyId, string? CustomerTypeCode, string? GroupCode, string? IndustryCode,
        string? CustomerPostingGroupCode, Guid? GLAccountId, string? ConstitutionCode, string? NationalityCode,
        string? TitleCode, string? MaritalStatusCode, string? CustomerBusinessTypeCode,
        string? CustomerBusinessSubTypeCode, string? IndustrySectorCode, string? SubSectorCode, string? FirstName,
        string? SecondName, string? ThirdName, string? FourthName, string? FifthName, string? SixthName,
        string? Notes, bool? IsVip, bool? IsSeniorMember, string? FinancialRequirement, string? Accommodation,
        bool? IsRelatedParty, string? RelationCode, string? EmployerEconomicActivityCode,
        IReadOnlyCollection<CustomerAddressRequest> Addresses, IndividualCustomerDetailsRequest? IndividualDetails,
        NonIndividualCustomerDetailsRequest? NonIndividualDetails, IReadOnlyCollection<BankAccountRequest> BankAccounts,
        IReadOnlyCollection<CustomerDocumentReferenceRequest> Documents, IReadOnlyCollection<CustomerTrackRequest> Tracks,
        IReadOnlyCollection<FactoringSubLimitRequest> FactoringSubLimits, bool CopyCommunicationAddressToPermanent);

    public sealed record CustomerQueryRequest(string? CustomerCode, string? Search, Guid? CompanyId, int Page = 1, int PageSize = 25);

    public sealed record CustomerDisplayProjection(string Code, string? Name);
    public sealed record CustomerReferenceProjection(string? Code, string? Name);
    public sealed record CustomerAddressResponse(CustomerAddressType AddressType, string? HouseNumber, string? BlockNumber,
        string? PostalCode, string? City, string? State, string? Country, string? POBoxNumber, string? WayNumber,
        string? Telephone, string? ResidenceFaxNumber, string? Mobile, string? Email, string? Website,
        string? ContactPerson, string? ContactPersonPhoneNumber, string? ContactPersonFlatNumber,
        string? AreaSheik, string? LocationLandmark);
    public sealed record BankAccountResponse(Guid Id, string? AccountTypeCode, string AccountNumber, string? BankCode,
        string? BankName, string? BankBranch, string? MICRCode, DateOnly? ClearingDate, bool IsDefault);
    public sealed record CustomerDocumentResponse(Guid Id, Guid DocumentDefinitionId, string DocumentCode,
        string? DocumentCategory, string? DocumentDescription, bool IsMandatory, bool? IsImageRequired,
        bool IsCollected, bool IsScanned, string? Remarks, string? Value, string? ValueSource,
        string? DmsReference, string? ViewMetadata);
    public sealed record CustomerTrackResponse(Guid Id, string? LineOfBusinessCode, string? AccountNumber,
        string TrackTypeCode, string? TrackTypeName, DateOnly TrackDate, string? Reason, string? ByLogin,
        DateOnly? ReleaseDate, string? ReleasedBy, CustomerTrackStatus Status);
    public sealed record CustomerCreditResponse(Guid CustomerId, string? LineOfBusiness, string? ProductGroup,
        decimal SanctionedAmount, decimal ApprovedAmount, decimal UtilizedAmount, decimal RemainingAmount,
        DateOnly? ValidUpTo, string? FacilityType);
    public sealed record FactoringSubLimitResponse(Guid Id, bool FactoringApplicable, string? FactoringTypeCode,
        string? FactoringTypeName, decimal? Limit, string? Covenant, string? CovenantClause, DateOnly? CutOffDate);

    public sealed record CustomerResponse(
        Guid Id, string CustomerCode, Guid CompanyId, bool IsCustomer, bool IsGuarantor, bool IsCoApplicant,
        string CustomerTypeCode, CustomerReferenceProjection? Group, string? GroupName, CustomerReferenceProjection? Industry,
        string? IndustryName, string? CustomerPostingGroupCode, Guid? GLAccountId, string? GLName,
        CustomerReferenceProjection? Constitution, string? ConstitutionName, CustomerReferenceProjection? Nationality,
        string? NationalityName, CustomerReferenceProjection? Title,
        CustomerReferenceProjection? MaritalStatus, CustomerReferenceProjection? CustomerBusinessType,
        CustomerReferenceProjection? CustomerBusinessSubType, CustomerReferenceProjection? IndustrySector,
        CustomerReferenceProjection? SubSector, string? FirstName, string? SecondName, string? ThirdName,
        string? FourthName, string? FifthName, string? SixthName, string? Notes, bool? IsVip,
        bool? IsSeniorMember, string? FinancialRequirement, string? Accommodation, bool? IsRelatedParty,
        string? RelationCode, string? EmployerEconomicActivityCode, CustomerLifecycleStatus Status,
        DateTime CreatedAtUtc, DateTime UpdatedAtUtc, IReadOnlyCollection<CustomerAddressResponse> Addresses,
        IndividualCustomerDetailsRequest? IndividualDetails, NonIndividualCustomerDetailsRequest? NonIndividualDetails,
        IReadOnlyCollection<BankAccountResponse> BankAccounts, IReadOnlyCollection<CustomerDocumentResponse> Documents,
        IReadOnlyCollection<CustomerTrackResponse> Tracks, CustomerCreditResponse? Credit,
        IReadOnlyCollection<FactoringSubLimitResponse> FactoringSubLimits);

    public sealed record LookupCatalogEntry(string Module, string LookupType, string Code, string Value, int DisplayOrder);
    public sealed record CustomerLookupCatalogResponse(IReadOnlyCollection<LookupCatalogEntry> Values);
    public sealed record CustomerCodeLookup(string CustomerCode, Guid CustomerId);
    public sealed record CopyCommunicationAddressRequest(string CustomerCode);
    public sealed record CopyCommunicationAddressResult(string CustomerCode, CustomerAddressResponse PermanentAddress);
    public sealed record CustomerApplicabilityContext(string CustomerTypeCode, string? OccupationCode,
        bool? IsSsEmployee, string? BusinessTypeCode, string? BusinessSubTypeCode,
        bool? FactoringApplicable, string? ConstitutionCode, string Mode);
    public enum CustomerFieldState { Required, Optional, ReadOnly, Unavailable }
    public sealed record CustomerFieldApplicability(string FieldName, CustomerFieldState State, string? Reason);
    public sealed record MasterReferenceValidationResult(
        IReadOnlyDictionary<string, CustomerReferenceProjection> References,
        IReadOnlyCollection<CustomerDocumentResponse> ApplicableDocuments,
        CustomerAddressResponse? CommunicationAddress,
        CustomerAddressResponse? PermanentAddress);
    ```
    - `CustomerCreateRequest` has no CustomerCode. Write requests contain codes/IDs and address values only; no caller-supplied authoritative display names. `CustomerResponse` is the only place where projection-only names such as `Group.Name`, `Nationality.Name`, or `GLName` may appear.
    - **Requirements:** 18.1–27.10.

- [x] 4. Create EF configurations, DbSets, DI registration, and migration command
  - [x] 4.1 CREATE `Finnova.Repository/Configuration/CustomerConfiguration.cs`, `CustomerAddressConfiguration.cs`, `IndividualCustomerDetailsConfiguration.cs`, `NonIndividualCustomerDetailsConfiguration.cs`, `BankAccountConfiguration.cs`, `CustomerDocumentReferenceConfiguration.cs`, `CustomerTrackEntryConfiguration.cs`, `FactoringSubLimitConfiguration.cs`, and `CustomerAuditEntryConfiguration.cs`.
    ```csharp
    // CREATE Finnova.Repository/Configuration/CustomerConfiguration.cs
    using Finnova.Models.Domain.Entities;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    namespace Finnova.Repository.Configuration;
    public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
    {
        public void Configure(EntityTypeBuilder<Customer> b)
        {
            b.ToTable("Customers"); b.HasKey(x => x.Id);
            b.Property(x => x.CustomerCode).HasMaxLength(12).IsRequired();
            b.HasIndex(x => x.CustomerCode).IsUnique();
            b.Property(x => x.CustomerTypeCode).HasMaxLength(50).IsRequired();
            b.Property(x => x.GroupCode).HasMaxLength(4);
            b.Property(x => x.IndustryCode).HasMaxLength(3);
            b.Property(x => x.CustomerPostingGroupCode).HasMaxLength(50);
            b.Property(x => x.ConstitutionCode).HasMaxLength(50);
            b.Property(x => x.NationalityCode).HasMaxLength(10);
            b.Property(x => x.TitleCode).HasMaxLength(50);
            b.Property(x => x.MaritalStatusCode).HasMaxLength(50);
            b.Property(x => x.CustomerBusinessTypeCode).HasMaxLength(50);
            b.Property(x => x.CustomerBusinessSubTypeCode).HasMaxLength(50);
            b.Property(x => x.IndustrySectorCode).HasMaxLength(50);
            b.Property(x => x.SubSectorCode).HasMaxLength(50);
            foreach (var p in new[] { nameof(Customer.FirstName), nameof(Customer.SecondName), nameof(Customer.ThirdName),
                nameof(Customer.FourthName), nameof(Customer.FifthName), nameof(Customer.SixthName) }) b.Property<string?>(p).HasMaxLength(100);
            b.Property(x => x.Notes).HasMaxLength(1000);
            b.Property(x => x.FinancialRequirement).HasMaxLength(240);
            b.Property(x => x.Accommodation).HasMaxLength(240);
            b.Property(x => x.RelationCode).HasMaxLength(50);
            b.Property(x => x.EmployerEconomicActivityCode).HasMaxLength(50);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            b.HasIndex(x => new { x.CompanyId, x.NationalityCode });
            // No GroupName, IndustryName, ConstitutionName, NationalityName, or GLName column.
        }
    }

    // CREATE Finnova.Repository/Configuration/CustomerAddressConfiguration.cs
    public sealed class CustomerAddressConfiguration : IEntityTypeConfiguration<CustomerAddress>
    {
        public void Configure(EntityTypeBuilder<CustomerAddress> b)
        {
            b.ToTable("CustomerAddresses"); b.HasKey(x => x.Id); b.Property(x => x.AddressType).HasConversion<string>().HasMaxLength(20);
            foreach (var p in new[] { nameof(CustomerAddress.HouseNumber), nameof(CustomerAddress.BlockNumber), nameof(CustomerAddress.City), nameof(CustomerAddress.State), nameof(CustomerAddress.Country), nameof(CustomerAddress.POBoxNumber), nameof(CustomerAddress.WayNumber), nameof(CustomerAddress.Telephone), nameof(CustomerAddress.ResidenceFaxNumber), nameof(CustomerAddress.Mobile), nameof(CustomerAddress.Email), nameof(CustomerAddress.Website), nameof(CustomerAddress.ContactPerson), nameof(CustomerAddress.ContactPersonPhoneNumber), nameof(CustomerAddress.ContactPersonFlatNumber), nameof(CustomerAddress.AreaSheik), nameof(CustomerAddress.LocationLandmark) }) b.Property<string?>(p).HasMaxLength(300);
            b.Property(x => x.PostalCode).HasMaxLength(6); b.HasIndex(x => new { x.CustomerId, x.AddressType }).IsUnique();
            b.HasIndex(x => x.CustomerId);
        }
    }

    // CREATE Finnova.Repository/Configuration/IndividualCustomerDetailsConfiguration.cs
    public sealed class IndividualCustomerDetailsConfiguration : IEntityTypeConfiguration<IndividualCustomerDetails>
    {
        public void Configure(EntityTypeBuilder<IndividualCustomerDetails> b)
        {
            b.ToTable("IndividualCustomerDetails"); b.HasKey(x => x.Id); b.HasIndex(x => x.CustomerId).IsUnique();
            b.Property(x => x.GenderCode).HasMaxLength(50); b.Property(x => x.SpouseName).HasMaxLength(150);
            b.Property(x => x.Qualification).HasMaxLength(150); b.Property(x => x.Profession).HasMaxLength(150);
            b.Property(x => x.IndiaIdentityType).HasMaxLength(50); b.Property(x => x.IndiaIdentityReference).HasMaxLength(200);
        }
    }

    // CREATE Finnova.Repository/Configuration/NonIndividualCustomerDetailsConfiguration.cs
    public sealed class NonIndividualCustomerDetailsConfiguration : IEntityTypeConfiguration<NonIndividualCustomerDetails>
    {
        public void Configure(EntityTypeBuilder<NonIndividualCustomerDetails> b)
        {
            b.ToTable("NonIndividualCustomerDetails"); b.HasKey(x => x.Id); b.HasIndex(x => x.CustomerId).IsUnique();
            b.Property(x => x.PublicOrCloselyHeldCode).HasMaxLength(50); b.Property(x => x.BusinessProfile).HasMaxLength(240);
            b.Property(x => x.GeographicalCoverage).HasMaxLength(120); b.Property(x => x.JVPartnerName).HasMaxLength(120);
            b.Property(x => x.CEOOrMDName).HasMaxLength(50); b.Property(x => x.ResidentialAddress).HasMaxLength(300);
            b.Property(x => x.CustomerBusinessTypeCode).HasMaxLength(50); b.Property(x => x.CustomerBusinessSubTypeCode).HasMaxLength(50);
            b.Property(x => x.IndustrySectorCode).HasMaxLength(50); b.Property(x => x.SubSectorCode).HasMaxLength(50);
            b.Property(x => x.BusinessActivities).HasMaxLength(100); b.Property(x => x.FactoringTypeCode).HasMaxLength(50);
        }
    }

    // CREATE Finnova.Repository/Configuration/BankAccountConfiguration.cs
    public sealed class BankAccountConfiguration : IEntityTypeConfiguration<BankAccount>
    {
        public void Configure(EntityTypeBuilder<BankAccount> b)
        {
            b.ToTable("CustomerBankAccounts"); b.HasKey(x => x.Id); b.Property(x => x.AccountNumber).HasMaxLength(16).IsRequired();
            b.Property(x => x.NormalizedDuplicateKey).HasMaxLength(400).IsRequired(); b.Property(x => x.AccountTypeCode).HasMaxLength(50);
            b.Property(x => x.BankCode).HasMaxLength(50); b.Property(x => x.MICRCode).HasMaxLength(10); b.Property(x => x.Address).HasMaxLength(300);
            b.Property(x => x.ContractNumber).HasMaxLength(15); b.Property(x => x.SubContractNumber).HasMaxLength(15);
            b.HasIndex(x => new { x.CustomerId, x.NormalizedDuplicateKey }).IsUnique(); b.HasIndex(x => x.CustomerId);
            b.HasIndex(x => x.CustomerId).HasFilter("[IsDefault] = 1").IsUnique();
        }
    }

    // CREATE Finnova.Repository/Configuration/CustomerDocumentReferenceConfiguration.cs
    public sealed class CustomerDocumentReferenceConfiguration : IEntityTypeConfiguration<CustomerDocumentReference>
    {
        public void Configure(EntityTypeBuilder<CustomerDocumentReference> b)
        {
            b.ToTable("CustomerDocumentReferences"); b.HasKey(x => x.Id); b.Property(x => x.ConstitutionCode).HasMaxLength(50).IsRequired();
            b.Property(x => x.DocumentCode).HasMaxLength(20).IsRequired(); b.Property(x => x.DocumentCategory).HasMaxLength(100);
            b.Property(x => x.DocumentDescription).HasMaxLength(500); b.Property(x => x.Remarks).HasMaxLength(1000);
            b.Property(x => x.Value).HasMaxLength(200); b.Property(x => x.ValueSource).HasMaxLength(50);
            b.Property(x => x.DmsReference).HasMaxLength(500); b.Property(x => x.ViewMetadata).HasMaxLength(2000);
            b.HasIndex(x => new { x.CustomerId, x.DocumentDefinitionId }).IsUnique(); b.HasIndex(x => x.CustomerId);
            // No byte[], varbinary, raw image, or duplicated Document definition columns.
        }
    }

    // CREATE Finnova.Repository/Configuration/CustomerTrackEntryConfiguration.cs
    public sealed class CustomerTrackEntryConfiguration : IEntityTypeConfiguration<CustomerTrackEntry>
    {
        public void Configure(EntityTypeBuilder<CustomerTrackEntry> b)
        {
            b.ToTable("CustomerTrackEntries"); b.HasKey(x => x.Id); b.Property(x => x.TrackTypeCode).HasMaxLength(50).IsRequired();
            b.Property(x => x.LineOfBusinessCode).HasMaxLength(50); b.Property(x => x.AccountNumber).HasMaxLength(16);
            b.Property(x => x.Reason).HasMaxLength(500); b.Property(x => x.ByLogin).HasMaxLength(100); b.Property(x => x.ReleasedBy).HasMaxLength(100);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            b.HasIndex(x => new { x.CustomerId, x.AccountNumber, x.TrackTypeCode }).IsUnique(); b.HasIndex(x => x.CustomerId);
        }
    }

    // CREATE Finnova.Repository/Configuration/FactoringSubLimitConfiguration.cs
    public sealed class FactoringSubLimitConfiguration : IEntityTypeConfiguration<FactoringSubLimit>
    {
        public void Configure(EntityTypeBuilder<FactoringSubLimit> b)
        {
            b.ToTable("CustomerFactoringSubLimits"); b.HasKey(x => x.Id); b.Property(x => x.FactoringTypeCode).HasMaxLength(50);
            b.Property(x => x.Covenant).HasMaxLength(500); b.Property(x => x.CovenantClause).HasMaxLength(1000);
            b.HasIndex(x => x.CustomerId);
        }
    }

    // CREATE Finnova.Repository/Configuration/CustomerAuditEntryConfiguration.cs
    public sealed class CustomerAuditEntryConfiguration : IEntityTypeConfiguration<CustomerAuditEntry>
    {
        public void Configure(EntityTypeBuilder<CustomerAuditEntry> b)
        {
            b.ToTable("CustomerAuditEntries"); b.HasKey(x => x.Id); b.Property(x => x.ActorId).HasMaxLength(100).IsRequired();
            b.Property(x => x.ChangedFieldsJson).HasMaxLength(8000).IsRequired(); b.Property(x => x.Action).HasConversion<string>().HasMaxLength(40);
            b.HasIndex(x => new { x.CustomerId, x.CreatedAtUtc }); b.HasIndex(x => x.ActorId);
        }
    }
    ```
    - These are separate tables, not a local master-data copy. If the project uses owned address objects instead, keep the same fields and unique `(CustomerId, AddressType)` constraint in the owned configuration; do not add an address-control master.
    - **Requirements:** 18.8–18.10, 19.1–19.10, 20.1–20.8, 23.1–23.10, 24.1–24.9, 25.1–25.8, 26.1–26.9, 27.1–27.4.

  - [x] 4.2 MODIFY `Finnova.Repository/Context/FinnovaDbContext.cs` and `Finnova.Repository/DependencyInjection.cs` at the bounded insertion points.
    ```csharp
    // MODIFY Finnova.Repository/Context/FinnovaDbContext.cs
    // Add with the other DbSet declarations, before OnModelCreating:
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerAddress> CustomerAddresses => Set<CustomerAddress>();
    public DbSet<IndividualCustomerDetails> IndividualCustomerDetails => Set<IndividualCustomerDetails>();
    public DbSet<NonIndividualCustomerDetails> NonIndividualCustomerDetails => Set<NonIndividualCustomerDetails>();
    public DbSet<BankAccount> CustomerBankAccounts => Set<BankAccount>();
    public DbSet<CustomerDocumentReference> CustomerDocumentReferences => Set<CustomerDocumentReference>();
    public DbSet<CustomerTrackEntry> CustomerTrackEntries => Set<CustomerTrackEntry>();
    public DbSet<FactoringSubLimit> CustomerFactoringSubLimits => Set<FactoringSubLimit>();
    public DbSet<CustomerAuditEntry> CustomerAuditEntries => Set<CustomerAuditEntry>();
    // Existing ApplyConfigurationsFromAssembly remains unchanged.

    // MODIFY Finnova.Repository/DependencyInjection.cs
    // The existing file already opens with `using Finnova.Repository.Interfaces;` and
    // `using Finnova.Repository.Repositories;`, so these types resolve without new usings.
    // Add with the existing scoped repository registrations. The interfaces and implementations
    // are the Customer repositories created in Task 4A (NOT Task 7).
    services.AddScoped<ICustomerRepository, CustomerRepository>();
    services.AddScoped<ICustomerAuditRepository, CustomerAuditRepository>();
    services.AddScoped<ICustomerReadRepository, CustomerReadRepository>();
    services.AddScoped<ICustomerUnitOfWork, CustomerUnitOfWork>();
    ```
    - Do not register a `LookupRepository`, `NationalityRepository`, `GLAccountRepository`, `DocumentRepository`, or Address Master owned by Customer. Those existing registrations remain the authoritative boundary registrations.
    - The `ICustomer*` resolver registrations (`ILookupReferenceResolver`, `ICustomerMasterReferenceValidator`, etc.) are the Service-owned types from Task 5.2. Keep the Task 4A repository registrations above them; see Task 5.2 for the resolver `using` requirement.

  - [x] 4.3 Create the migration command for the user to run after Tasks 1–4 are applied.
    ```powershell
    # USER-RUN ONLY; do not run during spec execution.
    dotnet ef migrations add AddCustomerOnboarding --project Finnova.Repository --startup-project Finnova.SystemAdminService --output-dir Migrations
    dotnet ef database update --project Finnova.Repository --startup-project Finnova.SystemAdminService
    ```
    - **Requirements:** 27.1–27.4. The migration must contain no Lookup, Nationality, GL, Document, or Address Control tables.

- [x] 4A. Create Customer repository/unit-of-work interfaces and EF Core implementations
  - [x] 4A.1 CREATE `Finnova.Repository/Interfaces/ICustomerRepository.cs` (repository, audit, read, unit-of-work, and transaction interfaces).
    - These interfaces belong in `Finnova.Repository.Interfaces` to match every existing repository (`INationalityRepository`, `IDocumentRepository`, ...) and because the Repository project cannot see types in `Finnova.Service`. The CQRS handlers in Task 7 consume them via `using Finnova.Repository.Interfaces;`. `CustomerQueryRequest` is already in `Finnova.Models.Contracts.Customers`, which the Repository project references, so the read interface can use it directly.
    ```csharp
    // CREATE Finnova.Repository/Interfaces/ICustomerRepository.cs
    using Finnova.Models.Contracts.Customers;
    using Finnova.Models.Domain.Entities;

    namespace Finnova.Repository.Interfaces;

    public interface ICustomerRepository
    {
        // Loads the full Customer aggregate (addresses, individual/non-individual details,
        // bank accounts, documents, tracks, factoring sub-limits) for Modify/child commands.
        Task<Customer?> GetByCodeAsync(string code, CancellationToken ct = default);
        Task<bool> ExistsByCodeAsync(string code, CancellationToken ct = default);
        Task<bool> ExistsByIdentityAsync(string normalizedIdentity, CancellationToken ct = default);
        Task AddAsync(Customer customer, CancellationToken ct = default);
    }

    public interface ICustomerAuditRepository
    {
        Task AddAsync(CustomerAuditEntry audit, CancellationToken ct = default);
    }

    public interface ICustomerReadRepository
    {
        Task<(IReadOnlyCollection<Customer> Items, int Total)> SearchAsync(
            CustomerQueryRequest request, CancellationToken ct = default);
    }

    public interface ICustomerTransaction : IAsyncDisposable
    {
        Task CommitAsync(CancellationToken ct = default);
        Task RollbackAsync(CancellationToken ct = default);
    }

    public interface ICustomerUnitOfWork
    {
        Task<int> SaveChangesAsync(CancellationToken ct = default);
        Task<ICustomerTransaction> BeginTransactionAsync(CancellationToken ct = default);
    }
    ```
    - `ExistsByIdentityAsync` matches against the identity the aggregate mapper normalizes in Task 7 (`ICustomerAggregateMapper.NormalizeIdentity`). The implementation below keys it on `IndiaIdentityReference` of the individual details; if the FRS normalization differs, align the predicate to the same normalization function rather than inventing a new one.
    - **Requirements:** 18.1–19.10, 27.1–27.10.

  - [x] 4A.2 CREATE `Finnova.Repository/Repositories/CustomerRepository.cs` (write/aggregate repository).
    - Follows the `RepositoryBase<T>` + `FinnovaDbContext` pattern used by every existing repository. The aggregate load uses `Include`/`ThenInclude` so Modify and child-record commands see the full graph. `AddAsync` only stages the aggregate; the unit of work owns `SaveChanges`/transaction (the base-class `AddAsync` saves immediately, so this override does NOT call the base).
    ```csharp
    // CREATE Finnova.Repository/Repositories/CustomerRepository.cs
    using Microsoft.EntityFrameworkCore;
    using Finnova.Models.Domain.Entities;
    using Finnova.Repository.Context;
    using Finnova.Repository.Interfaces;

    namespace Finnova.Repository.Repositories;

    public class CustomerRepository : RepositoryBase<Customer>, ICustomerRepository
    {
        public CustomerRepository(FinnovaDbContext context) : base(context) { }

        public async Task<Customer?> GetByCodeAsync(string code, CancellationToken ct = default)
        {
            var c = code.Trim();
            return await DbSet
                .Include(x => x.Addresses)
                .Include(x => x.IndividualDetails)
                .Include(x => x.NonIndividualDetails)
                .Include(x => x.BankAccounts)
                .Include(x => x.Documents)
                .Include(x => x.Tracks)
                .Include(x => x.FactoringSubLimits)
                .FirstOrDefaultAsync(x => x.CustomerCode == c, ct);   // tracked for Modify/child writes
        }

        public async Task<bool> ExistsByCodeAsync(string code, CancellationToken ct = default)
        {
            var c = code.Trim();
            return await DbSet.AsNoTracking().AnyAsync(x => x.CustomerCode == c, ct);
        }

        public async Task<bool> ExistsByIdentityAsync(string normalizedIdentity, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(normalizedIdentity)) return false;
            var key = normalizedIdentity.Trim();
            // Identity de-dup is keyed on the individual identity reference projected by the
            // aggregate mapper's NormalizeIdentity. Keep this predicate in step with that function.
            return await DbSet.AsNoTracking()
                .Where(x => x.IndividualDetails != null && x.IndividualDetails.IndiaIdentityReference != null)
                .AnyAsync(x => x.IndividualDetails!.IndiaIdentityReference == key, ct);
        }

        // Stages only. The feature unit of work commits Customer + children + audit in one transaction.
        public new async Task AddAsync(Customer customer, CancellationToken ct = default)
            => await DbSet.AddAsync(customer, ct);
    }
    ```
    - Do not store or query authoritative master display names here. The repository persists Customer-owned values and reference codes/IDs only.
    - **Requirements:** 18.1–19.10, 23.1–26.9, 27.1–27.10.

  - [x] 4A.3 CREATE `Finnova.Repository/Repositories/CustomerAuditRepository.cs` and `CustomerReadRepository.cs`.
    ```csharp
    // CREATE Finnova.Repository/Repositories/CustomerAuditRepository.cs
    using Finnova.Models.Domain.Entities;
    using Finnova.Repository.Context;
    using Finnova.Repository.Interfaces;

    namespace Finnova.Repository.Repositories;

    public class CustomerAuditRepository : RepositoryBase<CustomerAuditEntry>, ICustomerAuditRepository
    {
        public CustomerAuditRepository(FinnovaDbContext context) : base(context) { }

        // Stages only; the feature unit of work commits with the Customer aggregate.
        public new async Task AddAsync(CustomerAuditEntry audit, CancellationToken ct = default)
            => await DbSet.AddAsync(audit, ct);
    }
    ```
    ```csharp
    // CREATE Finnova.Repository/Repositories/CustomerReadRepository.cs
    using Microsoft.EntityFrameworkCore;
    using Finnova.Models.Contracts.Customers;
    using Finnova.Models.Domain.Entities;
    using Finnova.Repository.Context;
    using Finnova.Repository.Interfaces;

    namespace Finnova.Repository.Repositories;

    public class CustomerReadRepository : RepositoryBase<Customer>, ICustomerReadRepository
    {
        public CustomerReadRepository(FinnovaDbContext context) : base(context) { }

        public async Task<(IReadOnlyCollection<Customer> Items, int Total)> SearchAsync(
            CustomerQueryRequest request, CancellationToken ct = default)
        {
            var query = DbSet.AsNoTracking()
                .Include(x => x.Addresses)
                .Include(x => x.IndividualDetails)
                .Include(x => x.NonIndividualDetails)
                .Include(x => x.BankAccounts)
                .Include(x => x.Documents)
                .Include(x => x.Tracks)
                .Include(x => x.FactoringSubLimits)
                .AsQueryable();

            if (request.CompanyId is { } companyId)
                query = query.Where(x => x.CompanyId == companyId);

            if (!string.IsNullOrWhiteSpace(request.CustomerCode))
            {
                var code = request.CustomerCode.Trim();
                query = query.Where(x => x.CustomerCode == code);
            }

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var term = request.Search.Trim();
                // EF Core translates Contains to SQL LIKE; default CI collation keeps it case-insensitive.
                query = query.Where(x =>
                    x.CustomerCode.Contains(term) ||
                    (x.FirstName != null && x.FirstName.Contains(term)) ||
                    (x.SecondName != null && x.SecondName.Contains(term)));
            }

            var total = await query.CountAsync(ct);   // total before paging

            var page = request.Page < 1 ? 1 : request.Page;
            var pageSize = request.PageSize < 1 ? 25 : request.PageSize;

            var items = await query
                .OrderBy(x => x.CustomerCode)          // deterministic order
                .Skip((page - 1) * pageSize).Take(pageSize)
                .ToListAsync(ct);

            return (items, total);
        }
    }
    ```
    - The read repository returns the Customer aggregate only. Projection to `CustomerResponse` (including master display-name resolution) stays in `ICustomerProjectionMapper` from Task 7, so no master name is materialized here.
    - **Requirements:** 18.1–19.10, 27.1–27.10.

  - [x] 4A.4 CREATE `Finnova.Repository/Repositories/CustomerUnitOfWork.cs` (unit of work + EF transaction wrapper).
    - `BeginTransactionAsync` wraps `DbContext.Database.BeginTransactionAsync`. The handlers in Task 7 open the transaction, stage the aggregate/children/audit, call `SaveChangesAsync`, then `CommitAsync`. `DisposeAsync` rolls back any transaction not committed, so a failed master/address/applicability/duplicate check leaves no partial write.
    ```csharp
    // CREATE Finnova.Repository/Repositories/CustomerUnitOfWork.cs
    using Microsoft.EntityFrameworkCore.Storage;
    using Finnova.Repository.Context;
    using Finnova.Repository.Interfaces;

    namespace Finnova.Repository.Repositories;

    public class CustomerUnitOfWork : ICustomerUnitOfWork
    {
        private readonly FinnovaDbContext _context;
        public CustomerUnitOfWork(FinnovaDbContext context) => _context = context;

        public Task<int> SaveChangesAsync(CancellationToken ct = default)
            => _context.SaveChangesAsync(ct);

        public async Task<ICustomerTransaction> BeginTransactionAsync(CancellationToken ct = default)
        {
            var transaction = await _context.Database.BeginTransactionAsync(ct);
            return new CustomerTransaction(transaction);
        }

        private sealed class CustomerTransaction : ICustomerTransaction
        {
            private readonly IDbContextTransaction _transaction;
            private bool _completed;

            public CustomerTransaction(IDbContextTransaction transaction) => _transaction = transaction;

            public async Task CommitAsync(CancellationToken ct = default)
            {
                await _transaction.CommitAsync(ct);
                _completed = true;
            }

            public async Task RollbackAsync(CancellationToken ct = default)
            {
                await _transaction.RollbackAsync(ct);
                _completed = true;
            }

            public async ValueTask DisposeAsync()
            {
                if (!_completed)
                    await _transaction.RollbackAsync();   // no partial write on failure/early exit
                await _transaction.DisposeAsync();
            }
        }
    }
    ```
    - Register all four in `Finnova.Repository/DependencyInjection.cs` (already covered by Task 4.2's registration block). Do not register them a second time.
    - **Requirements:** 18.1–19.10, 27.1–27.10. One feature transaction covers Customer, child records, and audit entries.

- [x] 5. Create master-reference resolver interfaces, implementations, and DI registration
  - [x] 5.1 CREATE `Finnova.Service/Customers/References/CustomerLookupTypes.cs`, `LookupReferenceResolver.cs`, `NationalityReferenceResolver.cs`, `GLPostingGroupResolver.cs`, `DocumentDefinitionResolver.cs`, `IndiaAddressControl.cs`, and `CustomerMasterReferenceValidator.cs`.
    ```csharp
    // CREATE Finnova.Service/Customers/References/CustomerLookupTypes.cs
    namespace Finnova.Service.Customers.References;
    public static class CustomerLookupTypes
    {
        public const string Module = "<RESOLVE:LookupModule>";
        public const string Group = "<RESOLVE:GroupLookupType>";
        public const string Industry = "<RESOLVE:IndustryLookupType>";
        public const string Constitution = "<RESOLVE:ConstitutionLookupType>";
        public const string Title = "<RESOLVE:TitleLookupType>";
        public const string MaritalStatus = "MARITAL_STATUS"; // existing Origination/MARITAL_STATUS may be reused only if confirmed for Customer
        public const string CustomerBusinessType = "<RESOLVE:CustomerBusinessTypeLookupType>";
        public const string CustomerBusinessSubType = "<RESOLVE:CustomerBusinessSubTypeLookupType>";
        public const string IndustrySector = "<RESOLVE:IndustrySectorLookupType>";
        public const string SubSector = "<RESOLVE:SubSectorLookupType>";
        public const string TrackType = "<RESOLVE:TrackTypeLookupType>";
        public const string EntityRole = "<RESOLVE:CustomerDocumentEntityRole>";
    }

    // CREATE Finnova.Service/Customers/References/ReferenceProjections.cs
    using Finnova.Models.Contracts.Customers;
    namespace Finnova.Service.Customers.References;
    public sealed record LookupReferenceProjection(string Module, string LookupType, string Code, string Value);
    public sealed record NationalityReferenceProjection(string Code, string Name);
    public sealed record GLPostingGroupProjection(Guid Id, Guid CompanyId, string Code, string Name);
    public sealed record DocumentDefinitionProjection(Guid Id, string Code, string? Category, string? Description, bool IsMandatory, bool? IsImageRequired);
    public sealed record AddressControlResult(bool IsValid, IReadOnlyCollection<string> Errors, CustomerAddressResponse? ApprovedAddress);
    ```
    ```csharp
    // CREATE Finnova.Service/Customers/References/LookupReferenceResolver.cs
    using Finnova.Models.Contracts.Customers;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Repository.Interfaces;
    namespace Finnova.Service.Customers.References;
    public interface ILookupReferenceResolver
    {
        Task<LookupReferenceProjection> ResolveActiveAsync(string module, string lookupType, string code, string fieldName, CancellationToken ct = default);
        Task<IReadOnlyCollection<LookupReferenceProjection>> GetActiveAsync(string module, string lookupType, CancellationToken ct = default);
    }
    public sealed class LookupReferenceResolver(ILookupRepository repository) : ILookupReferenceResolver
    {
        public async Task<LookupReferenceProjection> ResolveActiveAsync(string module, string lookupType, string code, string fieldName, CancellationToken ct = default)
        {
            var values = await repository.GetActiveByModuleAndTypeAsync(module, lookupType, ct);
            var value = values.FirstOrDefault(x => string.Equals(x.Code.Trim(), code.Trim(), StringComparison.OrdinalIgnoreCase));
            return value is null
                ? throw new CustomerLookupReferenceNotFoundException(fieldName, module, lookupType, code)
                : new(module, lookupType, value.Code, value.Value);
        }
        public async Task<IReadOnlyCollection<LookupReferenceProjection>> GetActiveAsync(string module, string lookupType, CancellationToken ct = default)
            => (await repository.GetActiveByModuleAndTypeAsync(module, lookupType, ct)).Select(x => new LookupReferenceProjection(module, lookupType, x.Code, x.Value)).ToArray();
    }
    ```
    ```csharp
    // MODIFY Finnova.Repository/Interfaces/INationalityRepository.cs
    // Add to the existing interface:
    Task<Nationality?> GetActiveByCodeAsync(string code, CancellationToken ct = default);

    // MODIFY Finnova.Repository/Repositories/NationalityRepository.cs
    // Add to the existing class:
    public async Task<Nationality?> GetActiveByCodeAsync(string code, CancellationToken ct = default)
        => await DbSet.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive && x.Code == code.Trim(), ct);

    // CREATE Finnova.Service/Customers/References/NationalityReferenceResolver.cs
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Repository.Interfaces;
    namespace Finnova.Service.Customers.References;
    public interface INationalityReferenceResolver
    {
        Task<NationalityReferenceProjection> ResolveActiveAsync(string code, CancellationToken ct = default);
        Task<IReadOnlyCollection<NationalityReferenceProjection>> GetActiveAsync(CancellationToken ct = default);
    }
    public sealed class NationalityReferenceResolver(INationalityRepository repository) : INationalityReferenceResolver
    {
        public async Task<NationalityReferenceProjection> ResolveActiveAsync(string code, CancellationToken ct = default)
        {
            var row = await repository.GetActiveByCodeAsync(code, ct);
            return row is null ? throw new CustomerNationalityReferenceNotFoundException(code) : new(row.Code, row.Name);
        }
        public async Task<IReadOnlyCollection<NationalityReferenceProjection>> GetActiveAsync(CancellationToken ct = default)
            => (await repository.GetAllAsync(ct)).Where(x => x.IsActive).Select(x => new NationalityReferenceProjection(x.Code, x.Name)).ToArray();
    }
    ```
    ```csharp
    // MODIFY Finnova.Repository/Interfaces/IGLAccountRepository.cs
    // Add to the existing interface so Company scope and the active/approved/postable rule are enforced in SQL:
    Task<GLAccount?> GetActiveApprovedPostableByCompanyAndCodeAsync(Guid companyId, string code, CancellationToken ct = default);

    // MODIFY Finnova.Repository/Repositories/GLAccountRepository.cs
    // Add to the existing class:
    public async Task<GLAccount?> GetActiveApprovedPostableByCompanyAndCodeAsync(Guid companyId, string code, CancellationToken ct = default)
        => await DbSet.AsNoTracking().FirstOrDefaultAsync(x => x.CompanyId == companyId && x.GLCode == code.Trim() && x.Status == GLStatus.Active && x.AuthorizedVersionNumber > 0 && x.IsPostable, ct);

    // CREATE Finnova.Service/Customers/References/GLPostingGroupResolver.cs
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Repository.Interfaces;
    namespace Finnova.Service.Customers.References;
    public interface IGLPostingGroupResolver
    {
        Task<GLPostingGroupProjection> ResolveAsync(Guid companyId, string code, Guid? glAccountId, CancellationToken ct = default);
        Task<IReadOnlyCollection<GLPostingGroupProjection>> GetActiveAsync(Guid companyId, CancellationToken ct = default);
    }
    public sealed class GLPostingGroupResolver(IGLAccountRepository repository) : IGLPostingGroupResolver
    {
        public async Task<GLPostingGroupProjection> ResolveAsync(Guid companyId, string code, Guid? glAccountId, CancellationToken ct = default)
        {
            var row = await repository.GetActiveApprovedPostableByCompanyAndCodeAsync(companyId, code, ct);
            if (row is not null && glAccountId.HasValue && row.Id != glAccountId.Value) row = null;
            return row is null ? throw new CustomerGLPostingGroupInvalidException(companyId, code, "Account is not active, approved, postable, or Company-scoped.") : new(row.Id, row.CompanyId, row.GLCode, row.Name);
        }
        public async Task<IReadOnlyCollection<GLPostingGroupProjection>> GetActiveAsync(Guid companyId, CancellationToken ct = default)
            => (await repository.GetAllForCompanyAsync(companyId)).Where(x => x.Status == GLStatus.Active && x.AuthorizedVersionNumber > 0 && x.IsPostable).Select(x => new GLPostingGroupProjection(x.Id, x.CompanyId, x.GLCode, x.Name)).ToArray();
    }
    // If the existing GL contract provides a narrower approved/postable query, add that method to IGLAccountRepository and use it here; do not infer validity from Name or another Company.
    ```
    ```csharp
    // CREATE Finnova.Service/Customers/References/DocumentDefinitionResolver.cs
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Repository.Interfaces;
    namespace Finnova.Service.Customers.References;
    public interface IDocumentDefinitionResolver
    {
        Task<IReadOnlyCollection<DocumentDefinitionProjection>> GetApplicableAsync(string constitutionCode, string entityRole, CancellationToken ct = default);
        Task<DocumentDefinitionProjection> ResolveApplicableAsync(Guid documentId, string constitutionCode, string entityRole, CancellationToken ct = default);
    }
    public sealed class DocumentDefinitionResolver(IDocumentRepository repository) : IDocumentDefinitionResolver
    {
        public async Task<IReadOnlyCollection<DocumentDefinitionProjection>> GetApplicableAsync(string constitutionCode, string entityRole, CancellationToken ct = default)
            => (await repository.GetActiveForConsumptionAsync(constitutionCode, entityRole, ct)).Select(x => new DocumentDefinitionProjection(x.Id, x.DocCode, x.DocCategory, x.Description, x.IsMandatory, null)).ToArray();
        public async Task<DocumentDefinitionProjection> ResolveApplicableAsync(Guid documentId, string constitutionCode, string entityRole, CancellationToken ct = default)
        {
            var row = (await repository.GetActiveForConsumptionAsync(constitutionCode, entityRole, ct)).FirstOrDefault(x => x.Id == documentId);
            return row is null ? throw new CustomerDocumentDefinitionUnavailableException(constitutionCode, entityRole) : new(row.Id, row.DocCode, row.DocCategory, row.Description, row.IsMandatory, null);
        }
    }
    ```
    ```csharp
    // CREATE Finnova.Service/Customers/References/IndiaAddressControl.cs
    using Finnova.Models.Contracts.Customers;
    using Finnova.Models.Domain.Exceptions;
    namespace Finnova.Service.Customers.References;
    public interface IIndiaAddressControl
    {
        Task<AddressControlResult> ValidateAndApproveAsync(CustomerAddressRequest request, CancellationToken ct = default);
        Task<CustomerAddressResponse> CopyCommunicationToPermanentAsync(CustomerAddressResponse communication, CancellationToken ct = default);
    }
    public sealed class IndiaAddressControlDeferredAdapter : IIndiaAddressControl
    {
        public Task<AddressControlResult> ValidateAndApproveAsync(CustomerAddressRequest request, CancellationToken ct = default)
            => throw new CustomerBoundaryUnavailableException("India Address Control");
        public Task<CustomerAddressResponse> CopyCommunicationToPermanentAsync(CustomerAddressResponse communication, CancellationToken ct = default)
            => Task.FromResult(communication with { AddressType = Finnova.Models.Domain.Enums.CustomerAddressType.Permanent });
    }
    public sealed class IndiaAddressControlTestFake : IIndiaAddressControl
    {
        public Task<AddressControlResult> ValidateAndApproveAsync(CustomerAddressRequest r, CancellationToken ct = default)
            => Task.FromResult(new AddressControlResult(true, Array.Empty<string>(), new CustomerAddressResponse(r.AddressType, r.HouseNumber, r.BlockNumber, r.PostalCode, r.City, r.State, r.Country, r.POBoxNumber, r.WayNumber, r.Telephone, r.ResidenceFaxNumber, r.Mobile, r.Email, r.Website, r.ContactPerson, r.ContactPersonPhoneNumber, r.ContactPersonFlatNumber, r.AreaSheik, r.LocationLandmark)));
        public Task<CustomerAddressResponse> CopyCommunicationToPermanentAsync(CustomerAddressResponse c, CancellationToken ct = default)
            => Task.FromResult(c with { AddressType = Finnova.Models.Domain.Enums.CustomerAddressType.Permanent });
    }
    ```
    ```csharp
    // CREATE Finnova.Service/Customers/References/CustomerMasterReferenceValidator.cs
    using Finnova.Models.Contracts.Customers;
    using Finnova.Service.Customers.Validation;
    using Finnova.Models.Domain.Exceptions;
    namespace Finnova.Service.Customers.References;
    public interface ICustomerMasterReferenceValidator
    {
        Task<MasterReferenceValidationResult> ValidateAsync(CustomerCreateRequest request, CancellationToken ct = default);
        Task<MasterReferenceValidationResult> ValidateAsync(CustomerModifyRequest request, CustomerResponse current, CancellationToken ct = default);
    }
    public sealed class CustomerMasterReferenceValidator(
        ILookupReferenceResolver lookups, INationalityReferenceResolver nationalities,
        IGLPostingGroupResolver gl, IDocumentDefinitionResolver documents, IIndiaAddressControl addresses,
        CustomerFieldApplicabilityEvaluator applicability) : ICustomerMasterReferenceValidator
    {
        public Task<MasterReferenceValidationResult> ValidateAsync(CustomerCreateRequest r, CancellationToken ct = default)
            => ValidateCoreAsync(r.CompanyId, r.CustomerTypeCode, r.GroupCode, r.IndustryCode, r.CustomerPostingGroupCode, r.GLAccountId, r.ConstitutionCode, r.NationalityCode, r.TitleCode, r.MaritalStatusCode, r.CustomerBusinessTypeCode, r.CustomerBusinessSubTypeCode, r.IndustrySectorCode, r.SubSectorCode, r.Addresses, r.Documents, "Create", ct);
        public Task<MasterReferenceValidationResult> ValidateAsync(CustomerModifyRequest r, CustomerResponse current, CancellationToken ct = default)
            => ValidateCoreAsync(r.CompanyId, r.CustomerTypeCode ?? current.CustomerTypeCode, r.GroupCode, r.IndustryCode, r.CustomerPostingGroupCode, r.GLAccountId, r.ConstitutionCode, r.NationalityCode, r.TitleCode, r.MaritalStatusCode, r.CustomerBusinessTypeCode, r.CustomerBusinessSubTypeCode, r.IndustrySectorCode, r.SubSectorCode, r.Addresses, r.Documents, "Modify", ct);
        private async Task<MasterReferenceValidationResult> ValidateCoreAsync(Guid companyId, string customerType, string? group, string? industry, string? posting, Guid? glId, string? constitution, string? nationality, string? title, string? marital, string? businessType, string? businessSubType, string? sector, string? subSector, IReadOnlyCollection<CustomerAddressRequest> addressRequests, IReadOnlyCollection<CustomerDocumentReferenceRequest> documentRequests, string mode, CancellationToken ct)
        {
            var map = new Dictionary<string, CustomerReferenceProjection>();
            async Task Add(string key, string? code, string type) { if (!string.IsNullOrWhiteSpace(code)) { var x = await lookups.ResolveActiveAsync(CustomerLookupTypes.Module, type, code, key, ct); map[key] = new(x.Code, x.Value); } }
            await Add(nameof(group), group, CustomerLookupTypes.Group); await Add(nameof(industry), industry, CustomerLookupTypes.Industry); await Add(nameof(constitution), constitution, CustomerLookupTypes.Constitution); await Add(nameof(title), title, CustomerLookupTypes.Title); await Add(nameof(marital), marital, CustomerLookupTypes.MaritalStatus); await Add(nameof(businessType), businessType, CustomerLookupTypes.CustomerBusinessType); await Add(nameof(businessSubType), businessSubType, CustomerLookupTypes.CustomerBusinessSubType); await Add(nameof(sector), sector, CustomerLookupTypes.IndustrySector); await Add(nameof(subSector), subSector, CustomerLookupTypes.SubSector);
            if (!string.IsNullOrWhiteSpace(nationality)) { var n = await nationalities.ResolveActiveAsync(nationality, ct); map[nameof(nationality)] = new(n.Code, n.Name); }
            if (!string.IsNullOrWhiteSpace(posting)) { var g = await gl.ResolveAsync(companyId, posting, glId, ct); map[nameof(posting)] = new(g.Code, g.Name); }
            var applicable = string.IsNullOrWhiteSpace(constitution) ? Array.Empty<DocumentDefinitionProjection>() : await documents.GetApplicableAsync(constitution, CustomerLookupTypes.EntityRole, ct);
            var approvedAddresses = new List<CustomerAddressResponse>();
            foreach (var address in addressRequests) { var result = await addresses.ValidateAndApproveAsync(address, ct); if (!result.IsValid || result.ApprovedAddress is null) throw new CustomerAddressControlValidationException(result.Errors); approvedAddresses.Add(result.ApprovedAddress); }
            var requestedDocumentIds = documentRequests.Select(x => x.DocumentDefinitionId).ToHashSet();
            var mandatoryMissing = applicable.Where(x => x.IsMandatory && !requestedDocumentIds.Contains(x.Id)).ToArray();
            if (mandatoryMissing.Length > 0) throw new CustomerDocumentDefinitionUnavailableException(constitution!, CustomerLookupTypes.EntityRole);
            applicability.Validate(new CustomerApplicabilityContext(customerType, null, null, businessType, businessSubType, null, constitution, mode));
            return new(map, applicable.Select(x => new CustomerDocumentResponse(x.Id, x.Id, x.Code, x.Category, x.Description, x.IsMandatory, x.IsImageRequired, false, false, null, null, null, null, null)).ToArray(), approvedAddresses.FirstOrDefault(x => x.AddressType == Finnova.Models.Domain.Enums.CustomerAddressType.Communication), approvedAddresses.FirstOrDefault(x => x.AddressType == Finnova.Models.Domain.Enums.CustomerAddressType.Permanent));
        }
    }
    ```
    - The deferred Address Control adapter is intentionally explicit. Replace it only when the approved existing boundary path and contract are confirmed; do not create a second address master.
    - **Requirements:** 19.1–19.10, 20.1–20.8, 21.1–21.11, 22.1–22.8, 24.1–24.9, 25.1–25.8, 27.1–27.10.

  - [x] 5.2 MODIFY `Finnova.Repository/DependencyInjection.cs` and the SystemAdmin service registration to register every resolver.
    ```csharp
    // MODIFY the existing AddFinnovaRepository registration block:
    services.AddScoped<ILookupReferenceResolver, LookupReferenceResolver>();
    services.AddScoped<INationalityReferenceResolver, NationalityReferenceResolver>();
    services.AddScoped<IGLPostingGroupResolver, GLPostingGroupResolver>();
    services.AddScoped<IDocumentDefinitionResolver, DocumentDefinitionResolver>();
    services.AddScoped<IIndiaAddressControl, IndiaAddressControlDeferredAdapter>();
    services.AddScoped<ICustomerMasterReferenceValidator, CustomerMasterReferenceValidator>();
    // MODIFY Finnova.SystemAdminService/Program.cs after AddFinnovaRepository(connectionString):
    // services.AddScoped<CustomerFieldApplicabilityEvaluator>();
    // services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CreateCustomerCommand).Assembly));
    ```
    - **Requirements:** 27.1–27.10.

- [x] 6. Create pure helpers and validators
  - [x] 6.1 CREATE `Finnova.Service/Customers/Validation/CustomerRules.cs` with the complete pure rule set.
    ```csharp
    // CREATE Finnova.Service/Customers/Validation/CustomerRules.cs
    using System.Globalization;
    using System.Net.Mail;
    using System.Text.RegularExpressions;
    using Finnova.Models.Contracts.Customers;
    using Finnova.Models.Domain.Exceptions;
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
    ```
    - The helper set must validate exact FRS formats, DD/MM/YYYY boundaries, DOB-before-today, issue-before-expiry, numeric/non-negative/percentage/length/email/website/phone/PIN values, bank duplicate/default invariants, safe duplicate identity detection, and Track/Credit/Factoring prerequisites. It must not call external services.
    - **Requirements:** 18.1–18.10, 20.1–20.8, 21.1–21.11, 22.1–22.8, 23.1–23.10, 24.1–24.9, 25.1–25.8, 26.1–26.9, 27.5–27.10.

- [x] 7. Implement Customer Onboarding CQRS only
  - [x] 7.1 CREATE Customer repository/unit-of-work interfaces and Create/Modify/Query handlers under `Finnova.Service/Customers`.
    > **Repository/UoW interface placement (corrected).** The Customer repository, audit, read, unit-of-work, and transaction interfaces live in `Finnova.Repository.Interfaces` (alongside `INationalityRepository`, `IDocumentRepository`, etc.), and their concrete implementations live in `Finnova.Repository.Repositories` — created in **Task 4A**. They are NOT declared in `Finnova.Service`. The dependency direction is `Finnova.Service` → `Finnova.Repository` → `Finnova.Models`; the Repository project cannot see types declared in `Finnova.Service.Customers`, so interfaces consumed by a `Finnova.Repository`-hosted implementation must be declared in `Finnova.Repository`. The handlers below reference them through `using Finnova.Repository.Interfaces;`.
    >
    > Therefore **do not create `Finnova.Service/Customers/CustomerPorts.cs`**. The interfaces previously shown here (`ICustomerRepository`, `ICustomerAuditRepository`, `ICustomerReadRepository`, `ICustomerUnitOfWork`, `ICustomerTransaction`) are defined in Task 4A. `CustomerApplicationPorts.cs` (service-owned ports such as `IDocumentNumberGenerator`, `IAccountContractBoundary`, and the mappers) remains in `Finnova.Service.Customers` and is created below.
    ```csharp
    // CREATE Finnova.Service/Customers/CustomerApplicationPorts.cs
    using Finnova.Models.Contracts.Customers;
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;
    using Finnova.Service.Customers.References;
    namespace Finnova.Service.Customers;
    public interface IDocumentNumberGenerator { Task<string> GenerateAsync(string scope, int length, CancellationToken ct = default); }
    public interface IAccountContractBoundary
    {
        Task RequireModifyPrerequisiteAsync(Guid customerId, string? accountNumber, CancellationToken ct = default);
        Task<CustomerCreditResponse> GetCreditAsync(Guid customerId, CancellationToken ct = default);
        Task<CustomerCreditResponse> UpdateValidUpToAsync(Guid customerId, DateOnly? validUpTo, string actorId, CancellationToken ct = default);
    }
    public interface ICustomerAggregateMapper
    {
        string NormalizeIdentity(CustomerCreateRequest request);
        Customer Create(CustomerCreateRequest request, string customerCode, MasterReferenceValidationResult references);
        void ApplyModify(Customer customer, CustomerModifyRequest request, MasterReferenceValidationResult references);
        CustomerAuditEntry CreatedAudit(Customer customer, string actorId);
        CustomerAuditEntry ModifiedAudit(Customer customer, string actorId);
        CustomerResponse Response(Customer customer, MasterReferenceValidationResult references);
    }
    public interface ICustomerProjectionMapper { CustomerResponse Project(Customer customer); }
    public interface ICustomerChildMapper
    {
        BankAccountRequest Request(BankAccount account);
        Task AddBank(Customer customer, BankAccountRequest request, CancellationToken ct = default);
        Task ModifyBank(BankAccount account, BankAccountRequest request, CancellationToken ct = default);
        Task SetDefault(Customer customer, BankAccount account);
        Task ClearDefault(Customer customer);
        CustomerAuditEntry Audit(Customer customer, string actorId, CustomerAuditAction action);
        BankAccountResponse BankResponse(BankAccount account);
        CustomerDocumentReference AddDocument(Customer customer, CustomerDocumentReferenceRequest request, IReadOnlyCollection<DocumentDefinitionProjection> definitions);
        void ModifyDocument(CustomerDocumentReference row, CustomerDocumentReferenceRequest request, IReadOnlyCollection<DocumentDefinitionProjection> definitions);
        CustomerDocumentResponse DocumentResponse(CustomerDocumentReference row);
        CustomerTrackEntry AddTrack(Customer customer, CustomerTrackRequest request);
        void ReleaseTrack(CustomerTrackEntry row, string actorId);
        CustomerTrackResponse TrackResponse(CustomerTrackEntry row);
        FactoringSubLimit AddFactoring(Customer customer, FactoringSubLimitRequest request);
        void ModifyFactoring(FactoringSubLimit row, FactoringSubLimitRequest request);
        FactoringSubLimitResponse FactoringResponse(FactoringSubLimit row);
        CustomerAddressResponse AddressResponse(CustomerAddress row);
        void ReplacePermanent(Customer customer, CustomerAddressResponse address);
    }
    ```
    ```csharp
    // CREATE Finnova.Service/Customers/Commands/CreateCustomerCommand.cs
    using FluentValidation;
    using MediatR;
    using Finnova.Models.Contracts.Customers;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.Customers.Validation;
    using Finnova.Models.Domain.Exceptions;
    namespace Finnova.Service.Customers.Commands;
    public sealed record CreateCustomerCommand(CustomerCreateRequest Request, string ActorId) : IRequest<CustomerResponse>;
    public sealed class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
    {
        public CreateCustomerCommandValidator()
        {
            RuleFor(x => x.Request.CompanyId).NotEmpty(); RuleFor(x => x.Request.CustomerTypeCode).NotEmpty();
            RuleFor(x => x.Request.GroupCode).Must(x => x is null || System.Text.RegularExpressions.Regex.IsMatch(x, "^\\d{1,4}$"));
            RuleFor(x => x.Request.IndustryCode).MaximumLength(3); RuleFor(x => x.Request.Addresses).NotNull();
        }
    }
    public sealed class CreateCustomerCommandHandler(
        ICustomerRepository customers, ICustomerAuditRepository audits, ICustomerUnitOfWork uow,
        ICustomerMasterReferenceValidator references, IDocumentNumberGenerator customerCodeGenerator,
        ICustomerAggregateMapper mapper) : IRequestHandler<CreateCustomerCommand, CustomerResponse>
    {
        public async Task<CustomerResponse> Handle(CreateCustomerCommand message, CancellationToken ct)
        {
            CustomerFieldRules.NumericCode(message.Request.GroupCode, nameof(message.Request.GroupCode), 4);
            CustomerFieldRules.Length(message.Request.IndustryCode, nameof(message.Request.IndustryCode), 3);
            BankAccountRules.Validate(message.Request.BankAccounts);
            var refs = await references.ValidateAsync(message.Request, ct);
            var code = await customerCodeGenerator.GenerateAsync("CUSTOMER", 12, ct);
            CustomerCodeRules.ValidateGenerated(code);
            if (await customers.ExistsByCodeAsync(code, ct)) throw new CustomerCodeConflictException();
            var identity = mapper.NormalizeIdentity(message.Request);
            DuplicateIdentityRules.RejectIfExisting(await customers.ExistsByIdentityAsync(identity, ct));
            await using var transaction = await uow.BeginTransactionAsync(ct);
            var customer = mapper.Create(message.Request, code, refs);
            await customers.AddAsync(customer, ct);
            await audits.AddAsync(mapper.CreatedAudit(customer, message.ActorId), ct);
            await uow.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return mapper.Response(customer, refs);
        }
    }
    ```
    ```csharp
    // CREATE Finnova.Service/Customers/Commands/ModifyCustomerCommand.cs
    using FluentValidation;
    using MediatR;
    using Finnova.Models.Contracts.Customers;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.Customers.Validation;
    using Finnova.Models.Domain.Exceptions;
    namespace Finnova.Service.Customers.Commands;
    public sealed record ModifyCustomerCommand(CustomerModifyRequest Request, string ActorId) : IRequest<CustomerResponse>;
    public sealed class ModifyCustomerCommandValidator : AbstractValidator<ModifyCustomerCommand>
    {
        public ModifyCustomerCommandValidator() { RuleFor(x => x.Request.CustomerCode).Length(12); RuleFor(x => x.Request.CompanyId).NotEmpty(); }
    }
    public sealed class ModifyCustomerCommandHandler(
        ICustomerRepository customers, ICustomerAuditRepository audits, ICustomerUnitOfWork uow,
        ICustomerMasterReferenceValidator references, ICustomerAggregateMapper mapper) : IRequestHandler<ModifyCustomerCommand, CustomerResponse>
    {
        public async Task<CustomerResponse> Handle(ModifyCustomerCommand message, CancellationToken ct)
        {
            var current = await customers.GetByCodeAsync(message.Request.CustomerCode, ct) ?? throw new CustomerNotFoundException(message.Request.CustomerCode);
            CustomerCodeRules.EnsureImmutable(current.CustomerCode, message.Request.CustomerCode);
            if (current.CompanyId != message.Request.CompanyId) throw new CustomerNotFoundException(message.Request.CustomerCode);
            BankAccountRules.Validate(message.Request.BankAccounts);
            var currentProjection = mapper.Response(current, new MasterReferenceValidationResult(new Dictionary<string, CustomerReferenceProjection>(), Array.Empty<CustomerDocumentResponse>(), null, null));
            var refs = await references.ValidateAsync(message.Request, currentProjection, ct);
            await using var transaction = await uow.BeginTransactionAsync(ct);
            mapper.ApplyModify(current, message.Request, refs);
            await audits.AddAsync(mapper.ModifiedAudit(current, message.ActorId), ct);
            await uow.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return mapper.Response(current, refs);
        }
    }
    ```
    ```csharp
    // CREATE Finnova.Service/Customers/Queries/QueryCustomer.cs
    using MediatR;
    using Finnova.Models.Contracts.Customers;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.Customers.Validation;
    using Finnova.Models.Domain.Exceptions;
    namespace Finnova.Service.Customers.Queries;
    public sealed record QueryCustomer(CustomerQueryRequest Request) : IRequest<IReadOnlyCollection<CustomerResponse>>;
    public sealed class QueryCustomerHandler(ICustomerReadRepository read, ICustomerProjectionMapper mapper) : IRequestHandler<QueryCustomer, IReadOnlyCollection<CustomerResponse>>
    {
        public async Task<IReadOnlyCollection<CustomerResponse>> Handle(QueryCustomer q, CancellationToken ct)
        {
            var result = await read.SearchAsync(q.Request, ct);
            return result.Items.Select(mapper.Project).ToArray();
        }
    }
    ```
    - Implement `IDocumentNumberGenerator` by adapting the existing Document Number Control service; do not generate a customer code from `Guid`, user input, or an unapproved local counter. The handler must resolve all masters before mutation, use one transaction for Customer/children/audit, and never write caller display names.
    - **Requirements:** 18.1–19.10, 27.1–27.10.

  - [x] 7.2 CREATE Bank Account, Document, Track, Credit, and Factoring commands under `Finnova.Service/Customers/Commands`.
    ```csharp
    // CREATE Finnova.Service/Customers/Commands/CustomerChildCommands.cs
    using MediatR;
    using Finnova.Models.Contracts.Customers;
    using Finnova.Service.Customers.Validation;
    using Finnova.Models.Domain.Exceptions;
    namespace Finnova.Service.Customers.Commands;
    public sealed record AddBankAccountCommand(string CustomerCode, BankAccountRequest Request, string ActorId) : IRequest<BankAccountResponse>;
    public sealed record ModifyBankAccountCommand(string CustomerCode, Guid BankAccountId, BankAccountRequest Request, string ActorId) : IRequest<BankAccountResponse>;
    public sealed record SetDefaultBankAccountCommand(string CustomerCode, Guid BankAccountId, string ActorId) : IRequest<BankAccountResponse>;
    public sealed record ClearDefaultBankAccountCommand(string CustomerCode, string ActorId) : IRequest;
    public sealed record AddCustomerDocumentCommand(string CustomerCode, CustomerDocumentReferenceRequest Request, string ActorId) : IRequest<CustomerDocumentResponse>;
    public sealed record ModifyCustomerDocumentCommand(string CustomerCode, Guid DocumentId, CustomerDocumentReferenceRequest Request, string ActorId) : IRequest<CustomerDocumentResponse>;
    public sealed record AddCustomerTrackCommand(string CustomerCode, CustomerTrackRequest Request, string ActorId) : IRequest<CustomerTrackResponse>;
    public sealed record ReleaseCustomerTrackCommand(string CustomerCode, Guid TrackId, string ActorId) : IRequest<CustomerTrackResponse>;
    public sealed record UpdateCustomerCreditValidUpToCommand(string CustomerCode, DateOnly? ValidUpTo, string ActorId) : IRequest<CustomerCreditResponse>;
    public sealed record AddFactoringSubLimitCommand(string CustomerCode, FactoringSubLimitRequest Request, string ActorId) : IRequest<FactoringSubLimitResponse>;
    public sealed record ModifyFactoringSubLimitCommand(string CustomerCode, Guid SubLimitId, FactoringSubLimitRequest Request, string ActorId) : IRequest<FactoringSubLimitResponse>;
    public sealed record CopyCommunicationAddressCommand(string CustomerCode, string ActorId) : IRequest<CopyCommunicationAddressResult>;
    ```
    ```csharp
    // CREATE Finnova.Service/Customers/Commands/CustomerChildCommandHandler.cs
    using MediatR;
    using Finnova.Models.Contracts.Customers;
    using Finnova.Models.Domain.Enums;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.Customers.Validation;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Service.Customers.References;
    namespace Finnova.Service.Customers.Commands;
    public sealed class CustomerChildCommandHandler(
        ICustomerRepository customers, ICustomerUnitOfWork uow, ICustomerAuditRepository audits,
        ILookupReferenceResolver lookups, IDocumentDefinitionResolver documents, IIndiaAddressControl addresses,
        IAccountContractBoundary accountContract, ICustomerChildMapper mapper)
        : IRequestHandler<AddBankAccountCommand, BankAccountResponse>,
          IRequestHandler<ModifyBankAccountCommand, BankAccountResponse>,
          IRequestHandler<SetDefaultBankAccountCommand, BankAccountResponse>,
          IRequestHandler<ClearDefaultBankAccountCommand>,
          IRequestHandler<AddCustomerDocumentCommand, CustomerDocumentResponse>,
          IRequestHandler<ModifyCustomerDocumentCommand, CustomerDocumentResponse>,
          IRequestHandler<AddCustomerTrackCommand, CustomerTrackResponse>,
          IRequestHandler<ReleaseCustomerTrackCommand, CustomerTrackResponse>,
          IRequestHandler<UpdateCustomerCreditValidUpToCommand, CustomerCreditResponse>,
          IRequestHandler<AddFactoringSubLimitCommand, FactoringSubLimitResponse>,
          IRequestHandler<ModifyFactoringSubLimitCommand, FactoringSubLimitResponse>,
          IRequestHandler<CopyCommunicationAddressCommand, CopyCommunicationAddressResult>
    {
        public async Task<BankAccountResponse> Handle(AddBankAccountCommand x, CancellationToken ct) { var c = await Require(x.CustomerCode, ct); await using var transaction = await uow.BeginTransactionAsync(ct); var all = c.BankAccounts.ToArray(); BankAccountRules.Validate(all.Select(mapper.Request).Append(x.Request).ToArray()); await mapper.AddBank(c, x.Request, ct); await audits.AddAsync(mapper.Audit(c, x.ActorId, CustomerAuditAction.BankChanged), ct); await uow.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return mapper.BankResponse(c.BankAccounts[^1]); }
        public async Task<BankAccountResponse> Handle(ModifyBankAccountCommand x, CancellationToken ct) { var c = await Require(x.CustomerCode, ct); await using var transaction = await uow.BeginTransactionAsync(ct); var bank = c.BankAccounts.FirstOrDefault(b => b.Id == x.BankAccountId) ?? throw new CustomerChildRecordNotFoundException("bank account"); await mapper.ModifyBank(bank, x.Request, ct); BankAccountRules.Validate(c.BankAccounts.Select(mapper.Request).ToArray()); await audits.AddAsync(mapper.Audit(c, x.ActorId, CustomerAuditAction.BankChanged), ct); await uow.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return mapper.BankResponse(bank); }
        public async Task<BankAccountResponse> Handle(SetDefaultBankAccountCommand x, CancellationToken ct) { var c = await Require(x.CustomerCode, ct); await using var transaction = await uow.BeginTransactionAsync(ct); var bank = c.BankAccounts.FirstOrDefault(b => b.Id == x.BankAccountId) ?? throw new CustomerChildRecordNotFoundException("bank account"); await mapper.SetDefault(c, bank); await uow.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return mapper.BankResponse(bank); }
        public async Task Handle(ClearDefaultBankAccountCommand x, CancellationToken ct) { var c = await Require(x.CustomerCode, ct); await using var transaction = await uow.BeginTransactionAsync(ct); await mapper.ClearDefault(c); await uow.SaveChangesAsync(ct); await transaction.CommitAsync(ct); }
        public async Task<CustomerDocumentResponse> Handle(AddCustomerDocumentCommand x, CancellationToken ct) { var c = await Require(x.CustomerCode, ct); await using var transaction = await uow.BeginTransactionAsync(ct); var defs = await documents.GetApplicableAsync(x.Request.ConstitutionCode, CustomerLookupTypes.EntityRole, ct); DocumentRules.ValidateApplicable(x.Request.DocumentDefinitionId, defs); DocumentRules.RejectRawBinary(x.Request.Value); var row = mapper.AddDocument(c, x.Request, defs); await audits.AddAsync(mapper.Audit(c, x.ActorId, CustomerAuditAction.DocumentChanged), ct); await uow.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return mapper.DocumentResponse(row); }
        public async Task<CustomerDocumentResponse> Handle(ModifyCustomerDocumentCommand x, CancellationToken ct) { var c = await Require(x.CustomerCode, ct); await using var transaction = await uow.BeginTransactionAsync(ct); var row = c.Documents.FirstOrDefault(d => d.Id == x.DocumentId) ?? throw new CustomerChildRecordNotFoundException("document"); var defs = await documents.GetApplicableAsync(x.Request.ConstitutionCode, CustomerLookupTypes.EntityRole, ct); DocumentRules.ValidateApplicable(x.Request.DocumentDefinitionId, defs); mapper.ModifyDocument(row, x.Request, defs); await audits.AddAsync(mapper.Audit(c, x.ActorId, CustomerAuditAction.DocumentChanged), ct); await uow.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return mapper.DocumentResponse(row); }
        public async Task<CustomerTrackResponse> Handle(AddCustomerTrackCommand x, CancellationToken ct) { var c = await Require(x.CustomerCode, ct); await using var transaction = await uow.BeginTransactionAsync(ct); await accountContract.RequireModifyPrerequisiteAsync(c.Id, x.Request.AccountNumber, ct); await lookups.ResolveActiveAsync(CustomerLookupTypes.Module, CustomerLookupTypes.TrackType, x.Request.TrackTypeCode, nameof(x.Request.TrackTypeCode), ct); var row = mapper.AddTrack(c, x.Request); await audits.AddAsync(mapper.Audit(c, x.ActorId, CustomerAuditAction.TrackChanged), ct); await uow.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return mapper.TrackResponse(row); }
        public async Task<CustomerTrackResponse> Handle(ReleaseCustomerTrackCommand x, CancellationToken ct) { var c = await Require(x.CustomerCode, ct); await using var transaction = await uow.BeginTransactionAsync(ct); var row = c.Tracks.FirstOrDefault(t => t.Id == x.TrackId) ?? throw new CustomerChildRecordNotFoundException("track"); mapper.ReleaseTrack(row, x.ActorId); await audits.AddAsync(mapper.Audit(c, x.ActorId, CustomerAuditAction.TrackChanged), ct); await uow.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return mapper.TrackResponse(row); }
        public async Task<CustomerCreditResponse> Handle(UpdateCustomerCreditValidUpToCommand x, CancellationToken ct) { var c = await Require(x.CustomerCode, ct); await using var transaction = await uow.BeginTransactionAsync(ct); var credit = await accountContract.GetCreditAsync(c.Id, ct); CustomerPrerequisiteRules.RequireCreditValidUpToOnly(false); return await accountContract.UpdateValidUpToAsync(c.Id, x.ValidUpTo, x.ActorId, ct); }
        public async Task<FactoringSubLimitResponse> Handle(AddFactoringSubLimitCommand x, CancellationToken ct) { var c = await Require(x.CustomerCode, ct); await using var transaction = await uow.BeginTransactionAsync(ct); CustomerPrerequisiteRules.RequireFactoring(x.Request.FactoringApplicable, x.Request.FactoringTypeCode); var row = mapper.AddFactoring(c, x.Request); await audits.AddAsync(mapper.Audit(c, x.ActorId, CustomerAuditAction.FactoringChanged), ct); await uow.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return mapper.FactoringResponse(row); }
        public async Task<FactoringSubLimitResponse> Handle(ModifyFactoringSubLimitCommand x, CancellationToken ct) { var c = await Require(x.CustomerCode, ct); await using var transaction = await uow.BeginTransactionAsync(ct); var row = c.FactoringSubLimits.FirstOrDefault(r => r.Id == x.SubLimitId) ?? throw new CustomerChildRecordNotFoundException("factoring sub-limit"); CustomerPrerequisiteRules.RequireFactoring(x.Request.FactoringApplicable, x.Request.FactoringTypeCode); mapper.ModifyFactoring(row, x.Request); await audits.AddAsync(mapper.Audit(c, x.ActorId, CustomerAuditAction.FactoringChanged), ct); await uow.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return mapper.FactoringResponse(row); }
        public async Task<CopyCommunicationAddressResult> Handle(CopyCommunicationAddressCommand x, CancellationToken ct) { var c = await Require(x.CustomerCode, ct); await using var transaction = await uow.BeginTransactionAsync(ct); var communication = mapper.AddressResponse(c.Addresses.Single(a => a.AddressType == Finnova.Models.Domain.Enums.CustomerAddressType.Communication)); var permanent = await addresses.CopyCommunicationToPermanentAsync(communication, ct); mapper.ReplacePermanent(c, permanent); await uow.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return new(c.CustomerCode, permanent); }
        private async Task<Customer> Require(string code, CancellationToken ct) => await customers.GetByCodeAsync(code, ct) ?? throw new CustomerNotFoundException(code);
    }
    ```
    - `IAccountContractBoundary`, `ICustomerAggregateMapper`, and `ICustomerChildMapper` are explicit interfaces/adapters to existing account/contract data. They must not fabricate credit amounts, account validity, or bank-master values.
    - Track commands are Modify-only, resolve `TrackTypeCode`, and require the account/contract prerequisite. Credit exposes only `ValidUpTo` mutation. Factoring is conditional. Document commands use active applicable definitions and metadata/DMS references only.
    - **Requirements:** 23.1–26.9, 27.1–27.10.

- [x] 8. Create Customer Onboarding controllers, middleware branches, and gateway aliases
  - [x] 8.1 CREATE `Finnova.SystemAdminService/Controllers/CustomerOnboardingController.cs`.
    ```csharp
    // CREATE Finnova.SystemAdminService/Controllers/CustomerOnboardingController.cs
    using MediatR;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Finnova.Models.Contracts.Customers;
    using Finnova.Service.Customers.Commands;
    using Finnova.Service.Customers.Queries;
    namespace Finnova.SystemAdminService.Controllers;
    [ApiController, Authorize(Policy = "SystemAdmin"), Route("api/customer-onboarding")]
    public sealed class CustomerOnboardingController(ISender sender) : ControllerBase
    {
        [HttpPost] public Task<CustomerResponse> Create(CustomerCreateRequest request, CancellationToken ct) => sender.Send(new CreateCustomerCommand(request, User.Identity?.Name ?? "system"), ct);
        [HttpPut("{customerCode}")] public Task<CustomerResponse> Modify(string customerCode, CustomerModifyRequest request, CancellationToken ct) => sender.Send(new ModifyCustomerCommand(request with { CustomerCode = customerCode }, User.Identity?.Name ?? "system"), ct);
        [HttpGet] public Task<IReadOnlyCollection<CustomerResponse>> Query([FromQuery] CustomerQueryRequest request, CancellationToken ct) => sender.Send(new QueryCustomer(request), ct);
        [HttpPost("{customerCode}/banks")] public Task<BankAccountResponse> AddBank(string customerCode, BankAccountRequest request, CancellationToken ct) => sender.Send(new AddBankAccountCommand(customerCode, request, User.Identity?.Name ?? "system"), ct);
        [HttpPost("{customerCode}/banks/{bankId}/default")] public Task<BankAccountResponse> DefaultBank(string customerCode, Guid bankId, CancellationToken ct) => sender.Send(new SetDefaultBankAccountCommand(customerCode, bankId, User.Identity?.Name ?? "system"), ct);
        [HttpPost("{customerCode}/documents")] public Task<CustomerDocumentResponse> AddDocument(string customerCode, CustomerDocumentReferenceRequest request, CancellationToken ct) => sender.Send(new AddCustomerDocumentCommand(customerCode, request, User.Identity?.Name ?? "system"), ct);
        [HttpPost("{customerCode}/tracks")] public Task<CustomerTrackResponse> AddTrack(string customerCode, CustomerTrackRequest request, CancellationToken ct) => sender.Send(new AddCustomerTrackCommand(customerCode, request, User.Identity?.Name ?? "system"), ct);
        [HttpPost("{customerCode}/addresses/copy-communication")] public Task<CopyCommunicationAddressResult> CopyAddress(string customerCode, CancellationToken ct) => sender.Send(new CopyCommunicationAddressCommand(customerCode, User.Identity?.Name ?? "system"), ct);
        [HttpPatch("{customerCode}/credit/valid-up-to")] public Task<CustomerCreditResponse> UpdateCredit(string customerCode, DateOnly? validUpTo, CancellationToken ct) => sender.Send(new UpdateCustomerCreditValidUpToCommand(customerCode, validUpTo, User.Identity?.Name ?? "system"), ct);
        [HttpPost("{customerCode}/factoring-sub-limits")] public Task<FactoringSubLimitResponse> AddFactoring(string customerCode, FactoringSubLimitRequest request, CancellationToken ct) => sender.Send(new AddFactoringSubLimitCommand(customerCode, request, User.Identity?.Name ?? "system"), ct);
    }
    ```
    - Do not add Customer Reference or messaging endpoints. Query responses must apply authorization-aware projections and never serialize raw document content or hidden PII.

  - [x] 8.2 MODIFY `Finnova.SystemAdminService/Middleware/ExceptionHandlingMiddleware.cs` at the bounded insertion points below. Keep the existing middleware, existing non-Customer path precedence, RFC 7807 response construction, and all existing switch arms. Do not add a second middleware or replace the file wholesale.
    ```csharp
    // MODIFY Finnova.SystemAdminService/Middleware/ExceptionHandlingMiddleware.cs
    // INSERT immediately after the existing validationDocumentCode/fallbackDocumentCode declarations and before the exception switch:
    var isCustomer = ctx.Request.Path.StartsWithSegments(
        "/api/customer-onboarding", StringComparison.OrdinalIgnoreCase);
    var validationCustomerCode = isCustomer ? CustomerErrorCodes.Validation : validationDocumentCode;
    var fallbackCustomerCode = isCustomer ? CustomerErrorCodes.Unexpected : fallbackDocumentCode;

    // INSERT immediately before the existing shared FluentValidation.ValidationException arm:
    CustomerNotFoundException =>
        (StatusCodes.Status404NotFound, CustomerErrorCodes.NotFound, ex.Message),
    CustomerChildRecordNotFoundException =>
        (StatusCodes.Status404NotFound, CustomerErrorCodes.NotFound, ex.Message),
    CustomerCodeConflictException =>
        (StatusCodes.Status409Conflict, CustomerErrorCodes.Conflict, ex.Message),
    CustomerImmutableFieldException =>
        (StatusCodes.Status409Conflict, CustomerErrorCodes.Immutable, ex.Message),
    CustomerLookupReferenceNotFoundException =>
        (StatusCodes.Status400BadRequest, CustomerErrorCodes.LookupReference, ex.Message),
    CustomerNationalityReferenceNotFoundException =>
        (StatusCodes.Status400BadRequest, CustomerErrorCodes.NationalityReference, ex.Message),
    CustomerGLPostingGroupInvalidException =>
        (StatusCodes.Status400BadRequest, CustomerErrorCodes.GLReference, ex.Message),
    CustomerDocumentDefinitionUnavailableException =>
        (StatusCodes.Status400BadRequest, CustomerErrorCodes.DocumentReference, ex.Message),
    CustomerAddressControlValidationException =>
        (StatusCodes.Status400BadRequest, CustomerErrorCodes.AddressReference, ex.Message),
    CustomerConditionalApplicabilityException =>
        (StatusCodes.Status400BadRequest, CustomerErrorCodes.Validation, ex.Message),
    CustomerDuplicateReferenceException =>
        (StatusCodes.Status409Conflict, CustomerErrorCodes.DuplicateReference, ex.Message),
    CustomerDuplicateIdentityException =>
        (StatusCodes.Status409Conflict, CustomerErrorCodes.Duplicate, ex.Message),
    CustomerDuplicateBankAccountException =>
        (StatusCodes.Status409Conflict, CustomerErrorCodes.Duplicate, ex.Message),
    CustomerDefaultBankAccountException =>
        (StatusCodes.Status409Conflict, CustomerErrorCodes.DefaultBank, ex.Message),
    CustomerBoundaryUnavailableException =>
        (StatusCodes.Status503ServiceUnavailable, CustomerErrorCodes.BoundaryUnavailable, ex.Message),

    // MODIFY the existing shared fallback arms so Customer paths use the Customer family;
    // every non-Customer path continues using its existing selected family:
    FluentValidation.ValidationException v =>
        (StatusCodes.Status400BadRequest, validationCustomerCode, v.Message),
    _ =>
        (StatusCodes.Status500InternalServerError, fallbackCustomerCode, "Unexpected error.")
    ```
    The existing response writer remains unchanged: `ProblemDetails.Status`, `Title`, `Detail`, and `Extensions["code"]`. `Detail` may use the safe exception message only. Do not serialize Customer exception properties such as codes, CompanyId, account values, address errors, or document metadata. The current middleware does not add an `errors` extension; field-level details are included only where the existing shared validation contract already supplies them. Add `using Finnova.Models.Domain.Exceptions;` only if the file does not already contain it.
  - [x] 8.3 MODIFY `Finnova.ApiGateway/appsettings.json` with a confirmed route or bounded alias.
    ```json
    // MODIFY Finnova.ApiGateway/appsettings.json
    // Add to ReverseProxy.Routes. Resolve only the deployed UI-composed prefix and destination path.
    {
      "CustomerOnboardingAliasRoute": {
        "ClusterId": "systemadmin-cluster",
        "Match": { "Path": "<RESOLVE:UI-composed-customer-onboarding-prefix>/{**catch-all}" },
        "Transforms": [ { "PathPattern": "/api/customer-onboarding/{**catch-all}" } ]
      }
    }
    ```
    - If the UI calls `/api/systemadmin/**` directly, omit the alias and use the existing SystemAdmin route. Do not add a new host or collapse service boundaries.
    - **Requirements:** 2.1–4.6, 10.1–13.8, 18.1–18.4, 19.1–19.10, 23.1–26.9, 27.6–27.10.

- [x] 9. Add backend unit, property, and integration tests
  - [x] 9.1 CREATE `Finnova.Tests/Unit/CustomerOnboardingRulesTests.cs` and `Finnova.Tests/Unit/CustomerMasterReferenceValidatorTests.cs`.
    ```csharp
    // CREATE Finnova.Tests/Unit/CustomerOnboardingRulesTests.cs
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
            => (value.Length == 12 && value.All(char.IsLetterOrDigit)) == !Throws(() => CustomerCodeRules.ValidateGenerated(value));

        // Property 2 validates Requirements 3.1 and 27.5: normalization is idempotent.
        [Property(MaxTest = 100)]
        public bool Property2_Normalized_identity_is_idempotent(string value)
            => DuplicateIdentityRules.Normalize(DuplicateIdentityRules.Normalize(value)) == DuplicateIdentityRules.Normalize(value);

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
            => (value.Length != 6 || value[0] == '0' || value.Any(c => !char.IsDigit(c))) == Throws(() => CustomerFieldRules.IndianPin(value));

        // Property 6 validates Requirement 23.8: a non-empty bank set has exactly one default.
        [Property(MaxTest = 100)]
        public bool Property6_Default_account_count_is_one_when_present(bool first, bool second)
            => (first || second) && first != second ? !Throws(() => BankAccountRules.Validate(new[] { new BankAccountRequest(null, "1", "B", null, null, null, null, null, first), new BankAccountRequest(null, "2", "B", null, null, null, null, null, second) })) : true;

        // Property 7 validates Requirements 3.1 and 23.4: normalized identity is case-insensitive.
        [Property(MaxTest = 100)]
        public bool Property7_Code_normalization_is_case_insensitive(string value)
            => DuplicateIdentityRules.Normalize(value.ToLowerInvariant()) == DuplicateIdentityRules.Normalize(value.ToUpperInvariant());

        // Property 8 validates Requirements 21.8 and 27.5: issue date is strictly before expiry.
        [Property(MaxTest = 100)]
        public bool Property8_Date_rule_is_strict(DateOnly issue, DateOnly expiry)
            => (issue >= expiry) == Throws(() => CustomerFieldRules.IssueBeforeExpiry(issue, expiry));

        private static bool Throws(Action action) { try { action(); return false; } catch { return true; } }
    }
    ```
    ```csharp
    // CREATE Finnova.Tests/Unit/CustomerMasterReferenceValidatorTests.cs
    using Moq;
    using Xunit;
    using Finnova.Models.Domain.Entities;
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Repository.Interfaces;
    using Finnova.Service.Customers.References;
    public sealed class CustomerMasterReferenceValidatorTests
    {
        [Fact] public async Task Inactive_lookup_is_rejected() { var repo = new Mock<ILookupRepository>(); repo.Setup(x => x.GetActiveByModuleAndTypeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<LookupValue>()); var resolver = new LookupReferenceResolver(repo.Object); await Assert.ThrowsAsync<CustomerLookupReferenceNotFoundException>(() => resolver.ResolveActiveAsync("M", "T", "X", "GroupCode")); }
        [Fact] public async Task Nationality_resolver_uses_active_by_code() { var repo = new Mock<INationalityRepository>(); repo.Setup(x => x.GetActiveByCodeAsync("IN", It.IsAny<CancellationToken>())).ReturnsAsync(new Nationality { Code = "IN", Name = "India", IsActive = true }); var result = await new NationalityReferenceResolver(repo.Object).ResolveActiveAsync("IN"); Assert.Equal("India", result.Name); }
        [Fact] public async Task Document_resolver_uses_active_applicable_definitions() { var repo = new Mock<IDocumentRepository>(); repo.Setup(x => x.GetActiveForConsumptionAsync("C", "R", It.IsAny<CancellationToken>())).ReturnsAsync(new List<Document> { new() { Id = Guid.NewGuid(), DocCode = "PAN", IsMandatory = true } }); var result = await new DocumentDefinitionResolver(repo.Object).GetApplicableAsync("C", "R"); Assert.Single(result); }
        [Fact] public async Task Address_fake_returns_approved_values_without_external_call() { var fake = new IndiaAddressControlTestFake(); var result = await fake.ValidateAndApproveAsync(new CustomerAddressRequest(CustomerAddressType.Communication, "1", null, "110001", "Delhi", "Delhi", "India", null, null, null, null, null, "a@example.com", "https://example.com", null, null, null, null, null)); Assert.True(result.IsValid); }
    }
    ```
    - Correct the FsCheck property helper syntax to the project’s installed FsCheck.Xunit API while preserving the eight property identities, `MaxTest = 100`, and comments mapping Properties 1–8 to Requirements 18–27. Use boundary fakes only; never call external services.
    - **Requirements:** 18.1–27.10.

  - [x] 9.2 CREATE `Finnova.Tests/Unit/CustomerExceptionHandlingMiddlewareTests.cs`.
    ```csharp
    using System.Text.Json;
    using FluentValidation;
    using FluentValidation.Results;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.SystemAdminService.Middleware;
    using Microsoft.AspNetCore.Http;
    using Xunit;

    public sealed class CustomerExceptionHandlingMiddlewareTests
    {
        [Fact]
        public async Task Customer_validation_uses_customer_400_code()
            => await AssertResponseAsync("/api/customer-onboarding", new ValidationException(new[] { new ValidationFailure("GroupCode", "invalid") }), 400, "ERR-CUSTOMER-400");

        [Fact]
        public async Task Customer_not_found_uses_customer_404_code()
            => await AssertResponseAsync("/api/customer-onboarding/C001", new CustomerNotFoundException("C001"), 404, "ERR-CUSTOMER-404");

        [Fact]
        public async Task Customer_reference_failure_uses_semantic_code()
            => await AssertResponseAsync("/api/customer-onboarding", new CustomerLookupReferenceNotFoundException("GroupCode", "Customer", "Group", "1"), 400, "ERR-CUSTOMER-LOOKUP-REF");

        [Fact]
        public async Task Customer_conflict_uses_duplicate_code()
            => await AssertResponseAsync("/api/customer-onboarding", new CustomerDuplicateIdentityException(), 409, "ERR-CUSTOMER-DUPLICATE");

        [Fact]
        public async Task Customer_dependency_failure_uses_503_code()
            => await AssertResponseAsync("/api/customer-onboarding", new CustomerBoundaryUnavailableException("Lookup"), 503, "ERR-CUSTOMER-503");

        [Fact]
        public async Task Customer_unexpected_failure_uses_customer_500_code()
            => await AssertResponseAsync("/api/customer-onboarding", new InvalidOperationException("internal"), 500, "ERR-CUSTOMER-500");

        [Fact]
        public async Task Non_customer_path_keeps_existing_lookup_family()
            => await AssertResponseAsync("/api/lookup", new InvalidOperationException("internal"), 500, "ERR-LKP-500");

        private static async Task AssertResponseAsync(string path, Exception exception, int status, string code)
        {
            var context = new DefaultHttpContext();
            context.Request.Path = path;
            await using var body = new MemoryStream();
            context.Response.Body = body;

            var middleware = new ExceptionHandlingMiddleware(_ => throw exception);
            await middleware.InvokeAsync(context);

            Assert.Equal(status, context.Response.StatusCode);
            body.Position = 0;
            using var json = await JsonDocument.ParseAsync(body);
            Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
            Assert.Equal(code, json.RootElement.GetProperty("title").GetString());
        }
    }
    ```
    Cover Customer validation, not-found, typed reference, conflict, dependency, and unexpected mappings, plus regression of the existing non-Customer family. Assert only the stable status and code fields; do not expose or assert structured exception properties.
    - **Requirements:** 12.7–12.9, 27.6–27.10.
  - [x] 9.3 CREATE `Finnova.Tests/Integration/CustomerOnboardingApiTests.cs` with WebApplicationFactory coverage.
    ```csharp
    // CREATE Finnova.Tests/Integration/CustomerOnboardingApiTests.cs
    using System.Net;
    using System.Net.Http.Json;
    using Xunit;
    using Finnova.Models.Contracts.Customers;
    using Finnova.Models.Domain.Enums;
    using Finnova.Models.Domain.Exceptions;
    using Finnova.Models.Domain.Entities;
    using Finnova.Repository.Interfaces;
    public sealed class CustomerOnboardingApiTests : IClassFixture<SystemAdminAppFactory>
    {
        private readonly HttpClient _client;
        public CustomerOnboardingApiTests(SystemAdminAppFactory factory) => _client = factory.CreateClient();
        [Fact] public async Task Unauthorized_create_is_rejected() { var response = await _client.PostAsJsonAsync("/api/customer-onboarding", new { }); Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode); }
        [Fact] public async Task Unknown_lookup_returns_customer_validation_problem() { var request = new { companyId = Guid.NewGuid(), customerTypeCode = "X", groupCode = "9999", addresses = Array.Empty<object>() }; var response = await _client.PostAsJsonAsync("/api/customer-onboarding", request); Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized); }
        [Fact] public async Task Create_modify_query_flow_uses_generated_immutable_code() { /* Configure fake masters and auth in SystemAdminAppFactory; create without CustomerCode, modify with returned code, then query projection. */ Assert.True(true); }
        [Fact] public async Task Failed_child_validation_rolls_back_customer_and_audit() { /* Configure an invalid inactive Document/Address boundary and assert no Customer or audit row exists. */ Assert.True(true); }
        [Fact] public async Task Projection_resolves_current_names_without_persisting_them() { /* Rename a fake Lookup value, query, and assert the new projection while Customer storage remains code-only. */ Assert.True(true); }
    }
    ```
    - Replace the bounded comments with the repository’s existing `SystemAdminAppFactory`, auth setup, in-memory database, and test-fake registration. Cover authorization, Create/Modify/Query, inactive/unknown Lookup/Nationality/GL/Document/Address references, atomic rollback, audit, stale references, child invariants, and safe error responses.
    - **Requirements:** 18.1–27.10.

- [x] 10. Create frontend Customer Onboarding models and services
  - [x] 10.1 CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\models\customerOnboarding.ts` and `src/services/interfaces/customerOnboarding.service.ts`.
    ```typescript
    // CREATE src/models/customerOnboarding.ts
    export type CustomerOnboardingMode = 'create' | 'modify' | 'query';
    export type CustomerOnboardingSection = 'basic-address' | 'individual' | 'non-individual' | 'bank' | 'constitution-documents' | 'track' | 'credit' | 'factoring';
    export type CustomerAddressType = 'Communication' | 'Permanent';
    export interface LookupOption { module: string; lookupType: string; code: string; value: string; displayOrder: number; }
    export interface CustomerAddress { addressType: CustomerAddressType; houseNumber?: string; blockNumber?: string; postalCode?: string; city?: string; state?: string; country?: string; poBoxNumber?: string; wayNumber?: string; telephone?: string; residenceFaxNumber?: string; mobile?: string; email?: string; website?: string; contactPerson?: string; contactPersonPhoneNumber?: string; contactPersonFlatNumber?: string; areaSheik?: string; locationLandmark?: string; }
    export interface CustomerForm { customerCode?: string; companyId: string; isCustomer: boolean; isGuarantor: boolean; isCoApplicant: boolean; customerTypeCode: string; groupCode?: string; industryCode?: string; customerPostingGroupCode?: string; glAccountId?: string; constitutionCode?: string; nationalityCode?: string; titleCode?: string; maritalStatusCode?: string; customerBusinessTypeCode?: string; customerBusinessSubTypeCode?: string; industrySectorCode?: string; subSectorCode?: string; firstName?: string; secondName?: string; thirdName?: string; fourthName?: string; fifthName?: string; sixthName?: string; notes?: string; addresses: CustomerAddress[]; individualDetails?: Record<string, unknown>; nonIndividualDetails?: Record<string, unknown>; bankAccounts: BankAccountForm[]; documents: DocumentReferenceForm[]; tracks: TrackForm[]; factoringSubLimits: FactoringForm[]; }
    export interface BankAccountForm { id?: string; accountTypeCode?: string; accountNumber: string; bankCode?: string; bankName?: string; bankBranch?: string; micrCode?: string; clearingDate?: string; isDefault: boolean; }
    export interface DocumentReferenceForm { id?: string; documentDefinitionId: string; documentCode: string; documentCategory?: string; documentDescription?: string; isMandatory: boolean; isImageRequired: boolean; isCollected: boolean; isScanned: boolean; remarks?: string; value?: string; valueSource?: string; dmsReference?: string; viewMetadata?: string; }
    export interface TrackForm { id?: string; lineOfBusinessCode?: string; accountNumber?: string; trackTypeCode: string; trackTypeName?: string; trackDate: string; reason?: string; byLogin?: string; releaseDate?: string; releasedBy?: string; status: string; }
    export interface CreditDetails { customerId: string; lineOfBusiness?: string; productGroup?: string; sanctionedAmount: number; approvedAmount: number; utilizedAmount: number; remainingAmount: number; validUpTo?: string; facilityType?: string; }
    export interface FactoringForm { id?: string; factoringApplicable: boolean; factoringTypeCode?: string; factoringTypeName?: string; limit?: number; covenant?: string; covenantClause?: string; cutOffDate?: string; }
    export interface CustomerProjection extends CustomerForm { id: string; status: string; group?: { code: string; name?: string }; industry?: { code: string; name?: string }; constitution?: { code: string; name?: string }; nationality?: { code: string; name?: string }; postingGroupName?: string; credit?: CreditDetails; createdAtUtc: string; updatedAtUtc: string; }
    export interface CustomerCatalog { lookups: LookupOption[]; nationalities: LookupOption[]; glPostingGroups: LookupOption[]; documents: DocumentReferenceForm[]; }

    // CREATE src/services/interfaces/customerOnboarding.service.ts
    import type { CustomerCatalog, CustomerForm, CustomerOnboardingMode, CustomerProjection } from '../../models/customerOnboarding';
    export interface CustomerOnboardingService { loadCatalog(companyId: string): Promise<CustomerCatalog>; create(request: CustomerForm): Promise<CustomerProjection>; modify(customerCode: string, request: CustomerForm): Promise<CustomerProjection>; query(search?: string): Promise<CustomerProjection[]>; copyCommunicationAddress(customerCode: string): Promise<CustomerProjection>; }
    export const customerOnboardingModeAllowsWrite = (mode: CustomerOnboardingMode) => mode !== 'query';
    ```
    - Do not put final Lookup names or values in models. The response may contain current display projections, but the write model sends only codes/IDs and approved address values. The UI never sends `groupName`, `industryName`, `constitutionName`, `nationalityName`, or `glName` as authoritative fields.
    - **Requirements:** 18.1–20.8, 22.1–24.9, 27.1–27.10.

  - [x] 10.2 CREATE `src/services/real/customerOnboarding.service.ts`, `src/services/mock/customerOnboarding.service.ts`, and `src/services/customerOnboarding.service.ts`.
    ```typescript
    // CREATE src/services/real/customerOnboarding.service.ts
    import api from '../api';
    import type { CustomerCatalog, CustomerForm, CustomerProjection } from '../../models/customerOnboarding';
    import type { CustomerOnboardingService } from '../interfaces/customerOnboarding.service';
    export const realCustomerOnboardingService: CustomerOnboardingService = {
      async loadCatalog(companyId: string): Promise<CustomerCatalog> { const { data } = await api.get('/customer-onboarding/catalog', { params: { companyId } }); return data; },
      async create(request: CustomerForm): Promise<CustomerProjection> { const { data } = await api.post('/customer-onboarding', request); return data; },
      async modify(customerCode: string, request: CustomerForm): Promise<CustomerProjection> { const { data } = await api.put(`/customer-onboarding/${encodeURIComponent(customerCode)}`, { ...request, customerCode }); return data; },
      async query(search?: string): Promise<CustomerProjection[]> { const { data } = await api.get('/customer-onboarding', { params: { search } }); return data.items ?? data; },
      async copyCommunicationAddress(customerCode: string): Promise<CustomerProjection> { const { data } = await api.post(`/customer-onboarding/${encodeURIComponent(customerCode)}/addresses/copy-communication`); return data; },
    };

    // CREATE src/services/mock/customerOnboarding.service.ts
    import type { CustomerCatalog, CustomerForm, CustomerProjection } from '../../models/customerOnboarding';
    import type { CustomerOnboardingService } from '../interfaces/customerOnboarding.service';
    const lookup = (lookupType: string, code: string, value: string): import('../../models/customerOnboarding').LookupOption => ({ module: '<RESOLVE:LookupModule>', lookupType, code, value, displayOrder: 1 });
    const store: CustomerProjection[] = [];
    export const mockCustomerOnboardingService: CustomerOnboardingService = {
      async loadCatalog(): Promise<CustomerCatalog> { return { lookups: [lookup('MARITAL_STATUS', 'SINGLE', 'Single')], nationalities: [lookup('NATIONALITY', 'IN', 'India')], glPostingGroups: [], documents: [] }; },
      async create(request: CustomerForm): Promise<CustomerProjection> { const result = { ...request, id: crypto.randomUUID(), customerCode: 'MOCK00000001', status: 'Active', createdAtUtc: new Date().toISOString(), updatedAtUtc: new Date().toISOString() } as CustomerProjection; store.push(result); return result; },
      async modify(customerCode: string, request: CustomerForm): Promise<CustomerProjection> { const current = store.find(x => x.customerCode === customerCode); if (!current) throw new Error('Customer was not found.'); const result = { ...current, ...request, customerCode, updatedAtUtc: new Date().toISOString() }; Object.assign(current, result); return current; },
      async query(search?: string): Promise<CustomerProjection[]> { return search ? store.filter(x => x.customerCode?.includes(search)) : [...store]; },
      async copyCommunicationAddress(customerCode: string): Promise<CustomerProjection> { const current = store.find(x => x.customerCode === customerCode); if (!current) throw new Error('Customer was not found.'); const communication = current.addresses.find(x => x.addressType === 'Communication'); if (communication) current.addresses = [...current.addresses.filter(x => x.addressType !== 'Permanent'), { ...communication, addressType: 'Permanent' }]; return current; },
    };

    // CREATE src/services/customerOnboarding.service.ts
    import { realCustomerOnboardingService } from './real/customerOnboarding.service';
    import { mockCustomerOnboardingService } from './mock/customerOnboarding.service';
    export const customerOnboardingService = import.meta.env.VITE_USE_MOCK_API === 'true' ? mockCustomerOnboardingService : realCustomerOnboardingService;
    ```
    - Reuse the shared axios instance. Do not create an axios client, hard-code master values, or call repositories/external boundaries from the UI.
    - **Requirements:** 18.1–20.8, 22.1–24.9, 27.1–27.10.

- [x] 11. Create the Customer Onboarding page and tab components
  - [x] 11.1 CREATE `src/pages/CustomerOnboarding/CustomerOnboarding.tsx` and `src/pages/CustomerOnboarding/components/CustomerOnboardingTabs.tsx`.
    ```tsx
    // CREATE src/pages/CustomerOnboarding/CustomerOnboarding.tsx
    import { useCallback, useEffect, useState } from 'react';
    import { Alert, Box, Button, CircularProgress, Tabs, Tab, Typography } from '@mui/material';
    import type { CustomerForm, CustomerOnboardingMode, CustomerOnboardingSection, CustomerProjection } from '../../models/customerOnboarding';
    import { customerOnboardingService } from '../../services/customerOnboarding.service';
    import { CustomerOnboardingTabs } from './components/CustomerOnboardingTabs';
    export default function CustomerOnboarding({ mode = 'create', customerCode }: { mode?: CustomerOnboardingMode; customerCode?: string }) {
      const [model, setModel] = useState<CustomerProjection | CustomerForm>({ companyId: '', isCustomer: true, isGuarantor: false, isCoApplicant: false, customerTypeCode: '', addresses: [], bankAccounts: [], documents: [], tracks: [], factoringSubLimits: [] });
      const [section, setSection] = useState<CustomerOnboardingSection>('basic-address');
      const [busy, setBusy] = useState(false); const [error, setError] = useState<string>();
      useEffect(() => { if (!customerCode) return; setBusy(true); customerOnboardingService.query(customerCode).then(rows => rows[0] && setModel(rows[0])).catch(e => setError(e?.response?.data?.detail ?? e.message)).finally(() => setBusy(false)); }, [customerCode]);
      const save = useCallback(async () => { if (mode === 'query') return; setBusy(true); setError(undefined); try { const result = mode === 'create' ? await customerOnboardingService.create(model as CustomerForm) : await customerOnboardingService.modify(customerCode!, model as CustomerForm); setModel(result); } catch (e: any) { setError(e?.response?.data?.detail ?? e.message ?? 'Unable to save Customer.'); } finally { setBusy(false); } }, [customerCode, mode, model]);
      if (busy && !('id' in model)) return <CircularProgress />;
      return <Box p={2}><Typography variant="h4">Customer Onboarding</Typography>{error && <Alert severity="error">{error}</Alert>}<Tabs value={section} onChange={(_, value) => setSection(value)} variant="scrollable"><Tab value="basic-address" label="Basic & Address" /><Tab value="individual" label="Individual Details" /><Tab value="non-individual" label="Non-Individual Details" /><Tab value="bank" label="Bank Details" /><Tab value="constitution-documents" label="Constitution Documents" /><Tab value="track" label="Customer Track" /><Tab value="credit" label="Credit Details" /><Tab value="factoring" label="Factoring Sub-Limit" /></Tabs><CustomerOnboardingTabs section={section} mode={mode} value={model} onChange={setModel} /><Button disabled={mode === 'query' || busy} onClick={save}>{busy ? 'Saving…' : 'Save'}</Button></Box>;
    }

    // CREATE src/pages/CustomerOnboarding/components/CustomerOnboardingTabs.tsx
    import type { CustomerForm, CustomerOnboardingMode, CustomerOnboardingSection, CustomerProjection } from '../../../models/customerOnboarding';
    import { BasicAddressTab } from './BasicAddressTab'; import { IndividualDetailsTab } from './IndividualDetailsTab'; import { NonIndividualDetailsTab } from './NonIndividualDetailsTab'; import { BankDetailsTab } from './BankDetailsTab'; import { ConstitutionDocumentsTab } from './ConstitutionDocumentsTab'; import { CustomerTrackTab } from './CustomerTrackTab'; import { CustomerCreditDetailsTab } from './CustomerCreditDetailsTab'; import { FactoringSubLimitTab } from './FactoringSubLimitTab';
    export function CustomerOnboardingTabs({ section, mode, value, onChange }: { section: CustomerOnboardingSection; mode: CustomerOnboardingMode; value: CustomerForm | CustomerProjection; onChange: (value: CustomerForm | CustomerProjection) => void }) { const readOnly = mode === 'query'; switch (section) { case 'basic-address': return <BasicAddressTab value={value} readOnly={readOnly} onChange={onChange} />; case 'individual': return <IndividualDetailsTab value={value} readOnly={readOnly} onChange={onChange} />; case 'non-individual': return <NonIndividualDetailsTab value={value} readOnly={readOnly} onChange={onChange} />; case 'bank': return <BankDetailsTab value={value} readOnly={readOnly} onChange={onChange} />; case 'constitution-documents': return <ConstitutionDocumentsTab value={value} readOnly={readOnly} onChange={onChange} />; case 'track': return <CustomerTrackTab value={value} readOnly={readOnly} onChange={onChange} />; case 'credit': return <CustomerCreditDetailsTab value={value} readOnly={readOnly} onChange={onChange} />; case 'factoring': return <FactoringSubLimitTab value={value} readOnly={readOnly} onChange={onChange} />; } }
    ```
    - Query mode renders values read-only. Modify mode must disable CustomerCode and all FRS immutable/derived fields. Controlled values are selected from active catalog data, and the UI displays current response projections without sending display names back.

  - [x] 11.2 CREATE `BasicAddressTab.tsx`, `IndividualDetailsTab.tsx`, `NonIndividualDetailsTab.tsx`, and `BankDetailsTab.tsx`.
    ```tsx
    // CREATE src/pages/CustomerOnboarding/components/BasicAddressTab.tsx
    import { Stack, TextField } from '@mui/material'; import type { CustomerForm, CustomerProjection } from '../../../models/customerOnboarding';
    export function BasicAddressTab({ value, readOnly, onChange }: { value: CustomerForm | CustomerProjection; readOnly: boolean; onChange: (v: CustomerForm | CustomerProjection) => void }) { return <Stack spacing={2}><TextField label="Customer Code" value={value.customerCode ?? 'Generated on save'} InputProps={{ readOnly: true }} /><TextField label="Customer Type Code" value={value.customerTypeCode} disabled={readOnly} onChange={e => onChange({ ...value, customerTypeCode: e.target.value })} /><TextField label="Group Code" value={value.groupCode ?? ''} disabled={readOnly} onChange={e => onChange({ ...value, groupCode: e.target.value })} /><TextField label="Industry Code" value={value.industryCode ?? ''} disabled={readOnly} onChange={e => onChange({ ...value, industryCode: e.target.value })} /></Stack>; }

    // CREATE src/pages/CustomerOnboarding/components/IndividualDetailsTab.tsx
    import { Stack, TextField } from '@mui/material'; import type { CustomerForm, CustomerProjection } from '../../../models/customerOnboarding';
    export function IndividualDetailsTab({ value, readOnly, onChange }: { value: CustomerForm | CustomerProjection; readOnly: boolean; onChange: (v: CustomerForm | CustomerProjection) => void }) { const details = value.individualDetails ?? {}; return <Stack spacing={2}><TextField label="Date of Birth (DD/MM/YYYY)" value={String(details.dateOfBirth ?? '')} disabled={readOnly} onChange={e => onChange({ ...value, individualDetails: { ...details, dateOfBirth: e.target.value } })} /><TextField label="India Identity Type" value={String(details.indiaIdentityType ?? '')} disabled={readOnly} onChange={e => onChange({ ...value, individualDetails: { ...details, indiaIdentityType: e.target.value } })} /><TextField label="India Identity Reference" value={String(details.indiaIdentityReference ?? '')} disabled={readOnly} onChange={e => onChange({ ...value, individualDetails: { ...details, indiaIdentityReference: e.target.value } })} /></Stack>; }

    // CREATE src/pages/CustomerOnboarding/components/NonIndividualDetailsTab.tsx
    import { Stack, TextField } from '@mui/material'; import type { CustomerForm, CustomerProjection } from '../../../models/customerOnboarding';
    export function NonIndividualDetailsTab({ value, readOnly, onChange }: { value: CustomerForm | CustomerProjection; readOnly: boolean; onChange: (v: CustomerForm | CustomerProjection) => void }) { const details = value.nonIndividualDetails ?? {}; return <Stack spacing={2}><TextField label="Business Profile" value={String(details.businessProfile ?? '')} disabled={readOnly} onChange={e => onChange({ ...value, nonIndividualDetails: { ...details, businessProfile: e.target.value } })} /><TextField label="Business Activities" value={String(details.businessActivities ?? '')} disabled={readOnly} onChange={e => onChange({ ...value, nonIndividualDetails: { ...details, businessActivities: e.target.value } })} /></Stack>; }

    // CREATE src/pages/CustomerOnboarding/components/BankDetailsTab.tsx
    import { Checkbox, FormControlLabel, Stack, TextField } from '@mui/material'; import type { CustomerForm, CustomerProjection } from '../../../models/customerOnboarding';
    export function BankDetailsTab({ value, readOnly, onChange }: { value: CustomerForm | CustomerProjection; readOnly: boolean; onChange: (v: CustomerForm | CustomerProjection) => void }) { const rows = value.bankAccounts ?? []; return <Stack spacing={2}>{rows.map((row, i) => <Stack key={row.id ?? i} direction="row" spacing={1}><TextField label="Account Number" value={row.accountNumber} disabled={readOnly} onChange={e => { const next = [...rows]; next[i] = { ...row, accountNumber: e.target.value }; onChange({ ...value, bankAccounts: next }); }} /><FormControlLabel control={<Checkbox checked={row.isDefault} disabled={readOnly} />} label="Default" /></Stack>)}</Stack>; }
    ```
    - Add catalog-backed MUI selectors in these components using `loadCatalog`; do not replace selectors with hard-coded values. Preserve safe unsaved input on rejected save and show server field errors.

  - [x] 11.3 CREATE `ConstitutionDocumentsTab.tsx`, `CustomerTrackTab.tsx`, `CustomerCreditDetailsTab.tsx`, and `FactoringSubLimitTab.tsx`; MODIFY `App.tsx` and `Layout.tsx`.
    ```tsx
    // CREATE src/pages/CustomerOnboarding/components/ConstitutionDocumentsTab.tsx
    import { Stack, TextField, Typography } from '@mui/material'; import type { CustomerForm, CustomerProjection } from '../../../models/customerOnboarding';
    export function ConstitutionDocumentsTab({ value, readOnly }: { value: CustomerForm | CustomerProjection; readOnly: boolean; onChange: (v: CustomerForm | CustomerProjection) => void }) { return <Stack spacing={1}>{value.documents.map(x => <Stack key={x.id ?? x.documentDefinitionId}><Typography>{x.documentCode} {x.isMandatory ? '(Mandatory)' : ''}</Typography><TextField label="DMS Reference" value={x.dmsReference ?? ''} InputProps={{ readOnly: readOnly }} /></Stack>)}</Stack>; }

    // CREATE src/pages/CustomerOnboarding/components/CustomerTrackTab.tsx
    import { Stack, Typography } from '@mui/material'; import type { CustomerForm, CustomerProjection } from '../../../models/customerOnboarding';
    export function CustomerTrackTab({ value }: { value: CustomerForm | CustomerProjection; readOnly: boolean; onChange: (v: CustomerForm | CustomerProjection) => void }) { return <Stack spacing={1}>{value.tracks.map(x => <Typography key={x.id ?? `${x.trackTypeCode}-${x.trackDate}`}>{x.trackTypeName ?? x.trackTypeCode}: {x.status}</Typography>)}</Stack>; }

    // CREATE src/pages/CustomerOnboarding/components/CustomerCreditDetailsTab.tsx
    import { Stack, TextField } from '@mui/material'; import type { CustomerForm, CustomerProjection } from '../../../models/customerOnboarding';
    export function CustomerCreditDetailsTab({ value }: { value: CustomerForm | CustomerProjection; readOnly: boolean; onChange: (v: CustomerForm | CustomerProjection) => void }) { const credit = 'credit' in value ? value.credit : undefined; return <Stack spacing={2}><TextField label="Approved Amount" value={credit?.approvedAmount ?? ''} InputProps={{ readOnly: true }} /><TextField label="Valid Up To" value={credit?.validUpTo ?? ''} /></Stack>; }

    // CREATE src/pages/CustomerOnboarding/components/FactoringSubLimitTab.tsx
    import { Stack, Typography } from '@mui/material'; import type { CustomerForm, CustomerProjection } from '../../../models/customerOnboarding';
    export function FactoringSubLimitTab({ value }: { value: CustomerForm | CustomerProjection; readOnly: boolean; onChange: (v: CustomerForm | CustomerProjection) => void }) { return <Stack spacing={1}>{value.factoringSubLimits.map(x => <Typography key={x.id ?? x.factoringTypeCode}>{x.factoringTypeName ?? x.factoringTypeCode}: {x.limit ?? '—'}</Typography>)}</Stack>; }

    // MODIFY src/App.tsx: add the route with the existing router convention.
    <Route path="/customer-onboarding" element={<CustomerOnboarding mode="create" />} />
    <Route path="/customer-onboarding/:customerCode" element={<CustomerOnboarding mode="modify" />} />
    <Route path="/customer-onboarding/:customerCode/view" element={<CustomerOnboarding mode="query" />} />

    // MODIFY src/components/Layout.tsx: add one navigation item only.
    { label: 'Customer Onboarding', path: '/customer-onboarding' }
    ```
    - Do not add Customer Reference or MassTransit UI. Deferred integration states must be explicit and non-blocking. Display current projection names only as read values; never use them as write authority.
    - **Requirements:** 18.1–18.10, 19.1–19.10, 20.1–20.8, 21.1–26.9, 27.6–27.10.

- [x] 12. Add frontend tests and validation instructions
  - [x] 12.1 CREATE `src/pages/CustomerOnboarding/CustomerOnboarding.test.tsx` and `src/services/customerOnboarding.service.test.ts`.
    ```tsx
    // CREATE src/pages/CustomerOnboarding/CustomerOnboarding.test.tsx
    import { render, screen, fireEvent, waitFor } from '@testing-library/react';
    import { describe, expect, it, vi } from 'vitest';
    import CustomerOnboarding from './CustomerOnboarding';
    import { customerOnboardingService } from '../../services/customerOnboarding.service';
    vi.mock('../../services/customerOnboarding.service', () => ({ customerOnboardingService: { loadCatalog: vi.fn().mockResolvedValue({ lookups: [], nationalities: [], glPostingGroups: [], documents: [] }), create: vi.fn(), modify: vi.fn(), query: vi.fn().mockResolvedValue([]), copyCommunicationAddress: vi.fn() } }));
    describe('Customer Onboarding', () => {
      it('gates save in query mode', () => { render(<CustomerOnboarding mode="query" />); expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled(); });
      it('keeps Customer Code read-only in modify mode', async () => { (customerOnboardingService.query as any).mockResolvedValue([{ id: '1', customerCode: 'CUSTOMER0001', companyId: 'c', isCustomer: true, isGuarantor: false, isCoApplicant: false, customerTypeCode: 'T', addresses: [], bankAccounts: [], documents: [], tracks: [], factoringSubLimits: [], status: 'Active', createdAtUtc: '', updatedAtUtc: '' }]); render(<CustomerOnboarding mode="modify" customerCode="CUSTOMER0001" />); await waitFor(() => expect(screen.getByDisplayValue('CUSTOMER0001')).toHaveProperty('readOnly', true)); });
      it('does not send display names as write authority', async () => { const create = customerOnboardingService.create as any; create.mockResolvedValue({}); render(<CustomerOnboarding mode="create" />); fireEvent.change(screen.getByLabelText('Customer Type Code'), { target: { value: 'T' } }); fireEvent.click(screen.getByRole('button', { name: 'Save' })); await waitFor(() => expect(create).toHaveBeenCalled()); expect(create.mock.calls[0][0]).not.toHaveProperty('groupName'); });
      it('preserves active projection names as read values', async () => { render(<CustomerOnboarding mode="query" />); expect(screen.getByText('Customer Onboarding')).toBeInTheDocument(); });
      it('does not render deferred Customer Reference or messaging controls', () => { render(<CustomerOnboarding />); expect(screen.queryByText(/Customer Reference|Mind Mapping|MassTransit/i)).not.toBeInTheDocument(); });
    });

    // CREATE src/services/customerOnboarding.service.test.ts
    import { describe, expect, it, vi } from 'vitest';
    import { mockCustomerOnboardingService } from './mock/customerOnboarding.service';
    describe('Customer Onboarding service', () => {
      it('loads only boundary-backed catalog values', async () => { const catalog = await mockCustomerOnboardingService.loadCatalog('company'); expect(catalog.lookups.every(x => x.code && x.value)).toBe(true); });
      it('rejects stale modify references without changing CustomerCode', async () => { await expect(mockCustomerOnboardingService.modify('missing', { companyId: 'c', isCustomer: true, isGuarantor: false, isCoApplicant: false, customerTypeCode: 'T', addresses: [], bankAccounts: [], documents: [], tracks: [], factoringSubLimits: [] })).rejects.toThrow(); });
      it('copies communication address into permanent address without changing master values', async () => { const created = await mockCustomerOnboardingService.create({ companyId: 'c', isCustomer: true, isGuarantor: false, isCoApplicant: false, customerTypeCode: 'T', addresses: [{ addressType: 'Communication', postalCode: '110001' }], bankAccounts: [], documents: [], tracks: [], factoringSubLimits: [] }); const result = await mockCustomerOnboardingService.copyCommunicationAddress(created.customerCode!); expect(result.addresses.some(x => x.addressType === 'Permanent')).toBe(true); });
    });
    ```
    - Add cases for active/inactive/unknown master values, stale references, projection display names, CustomerCode immutability, child default/duplicate invariants, server error rendering, and safe unsaved input. Keep tests English-only and use test fakes; never call external services.
    - **Requirements:** 18.1–27.10.

  - [x] 12.2 Validate the applied implementation using user-run commands only.
    ```powershell
    # USER-RUN ONLY after copying all implementation tasks. Do not run during spec preparation.
    dotnet build Finnova.Backend.slnx
    dotnet test Finnova.Tests --configuration Release
    npm test -- --run
    npm run build
    ```
    - Resolve every `<RESOLVE:...>` seam before treating the implementation as complete. Confirm no source file contains deferred Customer Reference or MassTransit tasks, no Customer table contains authoritative display-name columns, no document table contains binary content, and no test calls an external service.
    - **Requirements:** 27.1–27.10.

## Notes

- Tasks marked with `*` are optional test tasks only if the project’s task runner supports optional subtasks. The code blocks remain copy-paste implementation templates.
- This document changes only the current Customer Onboarding slice. Do not modify `.config.kiro`, `requirements.md`, or `design.md` while applying this task rewrite.
- Resolve placeholders from approved contracts. Do not invent Lookup seeds, final module/type names, GL Company scope, Document entity roles, India identity values, routes, error prefixes, or external adapter paths.
- Customer-controlled values remain string codes. Existing `Origination/MARITAL_STATUS` can be consumed when confirmed, but no new Lookup values are seeded here.
- Use one feature transaction for Customer, child records, and audit entries. A failed master reference, address check, applicability check, duplicate check, or child invariant must leave no partial Customer write.
- The frontend uses the shared axios client and the `VITE_USE_MOCK_API` service toggle. It never owns master values and never sends display names as authoritative write fields.

## Deferred Follow-up Scope

The following are explicitly deferred and are not part of this copy-paste task list. They do not block Customer Onboarding implementation:

- Customer Reference persistence/management and its future audit/read APIs.
- MassTransit publisher/outbox/consumer Mind Mapping generation.
- PayU/Python KYC, behavior-model training, and AI inference.

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["0.1"] },
    { "id": 1, "tasks": ["1.1", "1.2", "2.1", "3.1"] },
    { "id": 2, "tasks": ["4.1", "4A.1", "5.1", "6.1"] },
    { "id": 3, "tasks": ["4.2", "4A.2", "4A.3", "4A.4", "5.2"] },
    { "id": 4, "tasks": ["4.3", "7.1"] },
    { "id": 5, "tasks": ["7.2", "8.1", "8.2", "8.3"] },
    { "id": 6, "tasks": ["9.1", "9.2", "10.1", "10.2"] },
    { "id": 7, "tasks": ["11.1", "11.2", "11.3"] },
    { "id": 8, "tasks": ["12.1"] },
    { "id": 9, "tasks": ["12.2"] }
  ]
}
```
