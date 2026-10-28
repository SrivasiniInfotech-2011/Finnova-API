# Implementation Tasks — Fix Customer Onboarding Integration Tests

## Scope and execution order

Apply one step at a time in `e:\Finnova\Finnova-API`. These tasks describe backend source changes only. Do not change `bin/` or `obj/`. Do not add frontend files. The snippets preserve the existing India-only, English-only, no-RTL product boundary.

The current source tree already contains much of the Customer slice from the implementation plan. Before applying a task, keep any existing implementation that matches the contract and apply only the listed replacement or insertion. For a larger file, the task gives the exact method or block to replace rather than reprinting unrelated code.

## Task 1 — MODIFY `Finnova.Service/Customers/CustomerPorts.cs`

Replace the complete file with the following content. Repository and unit-of-work interfaces are owned by `Finnova.Repository.Interfaces`; this file must not declare a second copy.

```csharp
// Customer repository and unit-of-work contracts are owned by Finnova.Repository.Interfaces.
// Application-owned Customer ports remain in CustomerApplicationPorts.cs.
```

## Task 2 — MODIFY Customer handler imports

In each listed file, add the repository contract import with the other using directives. Do not add a Service-local repository interface.

```csharp
// INSERT in the using section of each file below
using Finnova.Repository.Interfaces;
```

Files:

- `Finnova.Service/Customers/Commands/CreateCustomerCommand.cs`
- `Finnova.Service/Customers/Commands/ModifyCustomerCommand.cs`
- `Finnova.Service/Customers/Queries/QueryCustomer.cs`
- `Finnova.Service/Customers/Commands/CustomerChildCommandHandler.cs`

If a duplicate namespace import already resolves the same types, remove only the duplicate import after confirming the constructor types resolve to `Finnova.Repository.Interfaces.ICustomerRepository`, `ICustomerUnitOfWork`, and `ICustomerAuditRepository`.

## Task 3 — MODIFY `Finnova.Service/Customers/Commands/CustomerChildCommandHandler.cs`

Replace the complete clear-default handler method with this exact method. The command implements `IRequest`, so the handler return type is `Task`.

```csharp
public async Task Handle(ClearDefaultBankAccountCommand x, CancellationToken ct)
{
    await InTransactionAsync(x.CustomerCode, x.ActorId, CustomerAuditAction.BankChanged, ct, async customer =>
    {
        // This command remains subject to the existing exactly-one-default invariant.
        // It is not exposed by the current Customer Onboarding controller. A later
        // product decision must define zero-default behavior before this operation is
        // made public or the invariant is weakened.
        await mapper.ClearDefault(customer);
        BankAccountRules.Validate(customer.BankAccounts.Select(mapper.Request).ToArray());
        return Unit.Value;
    });
}
```

Do not change `IRequestHandler<ClearDefaultBankAccountCommand>` to a response-shaped handler and do not remove the existing `BankAccountRules.Validate` call in this ticket.

## Task 4 — MODIFY `Finnova.Tests/Infrastructure/InMemoryNationalityRepository.cs`

Insert or retain the following method inside `InMemoryNationalityRepository`. If a method with the same signature exists, replace it with this exact implementation.

```csharp
public Task<Nationality?> GetActiveByCodeAsync(
    string code,
    CancellationToken cancellationToken = default)
{
    var normalizedCode = code.Trim();
    var found = _store.FirstOrDefault(x =>
        x.IsActive &&
        string.Equals(x.Code, normalizedCode, StringComparison.OrdinalIgnoreCase));

    return Task.FromResult(found is null ? null : Clone(found));
}
```

Keep the existing clone helper and the repository-owned interface import:

```csharp
using Finnova.Models.Domain.Entities;
using Finnova.Repository.Interfaces;
```

The method must not return an inactive row or a reference to the stored mutable object.

## Task 5 — MODIFY Customer aggregate entities

Preserve EF-compatible private setters, but add explicit construction/update methods to each entity that the mapper must populate. Apply the following methods to the corresponding existing types. Do not add master display-name fields.

```csharp
// INSERT inside Customer
public static Customer Create(
    Guid companyId,
    bool isCustomer,
    bool isGuarantor,
    bool isCoApplicant,
    string customerTypeCode,
    string customerCode)
{
    var customer = new Customer
    {
        CompanyId = companyId,
        IsCustomer = isCustomer,
        IsGuarantor = isGuarantor,
        IsCoApplicant = isCoApplicant,
        CustomerTypeCode = customerTypeCode,
        CustomerCode = customerCode,
    };
    return customer;
}

public void ApplyCore(
    string customerTypeCode,
    string? groupCode,
    string? industryCode,
    string? customerPostingGroupCode,
    Guid? glAccountId,
    string? constitutionCode,
    string? nationalityCode,
    string? titleCode,
    string? maritalStatusCode,
    string? customerBusinessTypeCode,
    string? customerBusinessSubTypeCode,
    string? industrySectorCode,
    string? subSectorCode,
    string? firstName,
    string? secondName,
    string? thirdName,
    string? fourthName,
    string? fifthName,
    string? sixthName,
    string? notes,
    bool? isVip,
    bool? isSeniorMember,
    string? financialRequirement,
    string? accommodation,
    bool? isRelatedParty,
    string? relationCode,
    string? employerEconomicActivityCode)
{
    CustomerTypeCode = customerTypeCode;
    GroupCode = groupCode;
    IndustryCode = industryCode;
    CustomerPostingGroupCode = customerPostingGroupCode;
    GLAccountId = glAccountId;
    ConstitutionCode = constitutionCode;
    NationalityCode = nationalityCode;
    TitleCode = titleCode;
    MaritalStatusCode = maritalStatusCode;
    CustomerBusinessTypeCode = customerBusinessTypeCode;
    CustomerBusinessSubTypeCode = customerBusinessSubTypeCode;
    IndustrySectorCode = industrySectorCode;
    SubSectorCode = subSectorCode;
    FirstName = firstName;
    SecondName = secondName;
    ThirdName = thirdName;
    FourthName = fourthName;
    FifthName = fifthName;
    SixthName = sixthName;
    Notes = notes;
    IsVip = isVip;
    IsSeniorMember = isSeniorMember;
    FinancialRequirement = financialRequirement;
    Accommodation = accommodation;
    IsRelatedParty = isRelatedParty;
    RelationCode = relationCode;
    EmployerEconomicActivityCode = employerEconomicActivityCode;
    Touch();
}

public void ReplaceAddresses(IEnumerable<CustomerAddress> addresses)
{
    Addresses.Clear();
    Addresses.AddRange(addresses);
    Touch();
}

public void ReplaceIndividualDetails(IndividualCustomerDetails? details)
{
    IndividualDetails = details;
    Touch();
}

public void ReplaceNonIndividualDetails(NonIndividualCustomerDetails? details)
{
    NonIndividualDetails = details;
    Touch();
}

public void ReplaceChildren(
    IEnumerable<BankAccount> bankAccounts,
    IEnumerable<CustomerDocumentReference> documents,
    IEnumerable<CustomerTrackEntry> tracks,
    IEnumerable<FactoringSubLimit> factoringSubLimits)
{
    BankAccounts.Clear();
    BankAccounts.AddRange(bankAccounts);
    Documents.Clear();
    Documents.AddRange(documents);
    Tracks.Clear();
    Tracks.AddRange(tracks);
    FactoringSubLimits.Clear();
    FactoringSubLimits.AddRange(factoringSubLimits);
    Touch();
}
```

Add initialization methods with the same assignment semantics to `CustomerAddress`, `IndividualCustomerDetails`, `NonIndividualCustomerDetails`, `BankAccount`, `CustomerDocumentReference`, `CustomerTrackEntry`, and `FactoringSubLimit`. Use the existing property lists and request records as the parameter source. Each method must assign only Customer-owned values, codes, IDs, and approved address/DMS metadata. Do not add `GroupName`, `IndustryName`, `ConstitutionName`, `NationalityName`, or `GLName` properties.

For `BankAccount`, the initialization method must calculate the duplicate key from the Customer mapper and set `IsDefault`; it must not validate the collection by itself. Collection validation remains in `BankAccountRules` and the handlers.

## Task 6 — CREATE `Finnova.Service/Customers/CustomerAggregateMapper.cs`

Create this file. Use the existing request records, `MasterReferenceValidationResult`, aggregate factory methods from Task 5, and existing response constructors. The mapper must not call a master repository directly.

```csharp
using System.Text.Json;
using Finnova.Models.Contracts.Customers;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Service.Customers.References;
using Finnova.Service.Customers.Validation;

namespace Finnova.Service.Customers;

public sealed class CustomerAggregateMapper : ICustomerAggregateMapper
{
    public string NormalizeIdentity(CustomerCreateRequest request)
        => DuplicateIdentityRules.Normalize(
            string.Join(" ", new[] { request.FirstName, request.SecondName, request.ThirdName }
                .Where(x => !string.IsNullOrWhiteSpace(x))));

    public Customer Create(
        CustomerCreateRequest request,
        string customerCode,
        MasterReferenceValidationResult references)
    {
        var customer = Customer.Create(
            request.CompanyId,
            request.IsCustomer,
            request.IsGuarantor,
            request.IsCoApplicant,
            request.CustomerTypeCode,
            customerCode);

        ApplyCore(customer, request);
        customer.ReplaceAddresses(request.Addresses.Select(CreateAddress));
        customer.ReplaceChildren(
            request.BankAccounts.Select(CreateBank).ToArray(),
            Array.Empty<CustomerDocumentReference>(),
            Array.Empty<CustomerTrackEntry>(),
            request.FactoringSubLimits.Select(CreateFactoring).ToArray());
        return customer;
    }

    public void ApplyModify(
        Customer customer,
        CustomerModifyRequest request,
        MasterReferenceValidationResult references)
    {
        CustomerCodeRules.EnsureImmutable(customer.CustomerCode, request.CustomerCode);
        ApplyCore(customer, request);
        customer.ReplaceAddresses(request.Addresses.Select(CreateAddress));
        customer.ReplaceChildren(
            request.BankAccounts.Select(CreateBank).ToArray(),
            request.Documents.Select(CreateDocument).ToArray(),
            request.Tracks.Select(CreateTrack).ToArray(),
            request.FactoringSubLimits.Select(CreateFactoring).ToArray());
    }

    public CustomerAuditEntry CreatedAudit(Customer customer, string actorId)
        => Audit(customer, actorId, CustomerAuditAction.Created);

    public CustomerAuditEntry ModifiedAudit(Customer customer, string actorId)
        => Audit(customer, actorId, CustomerAuditAction.Modified);

    public CustomerResponse Response(
        Customer customer,
        MasterReferenceValidationResult references)
        => CustomerResponseMapper.Map(customer, references);

    private static void ApplyCore(Customer customer, CustomerCreateRequest request)
        => customer.ApplyCore(
            request.CustomerTypeCode, request.GroupCode, request.IndustryCode,
            request.CustomerPostingGroupCode, request.GLAccountId, request.ConstitutionCode,
            request.NationalityCode, request.TitleCode, request.MaritalStatusCode,
            request.CustomerBusinessTypeCode, request.CustomerBusinessSubTypeCode,
            request.IndustrySectorCode, request.SubSectorCode, request.FirstName,
            request.SecondName, request.ThirdName, request.FourthName, request.FifthName,
            request.SixthName, request.Notes, request.IsVip, request.IsSeniorMember,
            request.FinancialRequirement, request.Accommodation, request.IsRelatedParty,
            request.RelationCode, request.EmployerEconomicActivityCode);

    private static void ApplyCore(Customer customer, CustomerModifyRequest request)
        => customer.ApplyCore(
            request.CustomerTypeCode, request.GroupCode, request.IndustryCode,
            request.CustomerPostingGroupCode, request.GLAccountId, request.ConstitutionCode,
            request.NationalityCode, request.TitleCode, request.MaritalStatusCode,
            request.CustomerBusinessTypeCode, request.CustomerBusinessSubTypeCode,
            request.IndustrySectorCode, request.SubSectorCode, request.FirstName,
            request.SecondName, request.ThirdName, request.FourthName, request.FifthName,
            request.SixthName, request.Notes, request.IsVip, request.IsSeniorMember,
            request.FinancialRequirement, request.Accommodation, request.IsRelatedParty,
            request.RelationCode, request.EmployerEconomicActivityCode);

    private static CustomerAuditEntry Audit(Customer customer, string actorId, CustomerAuditAction action)
        => CustomerAuditEntry.Create(customer.Id, action, actorId,
            JsonSerializer.Serialize(new { action, customer.CustomerCode }));

    // Use the existing entity/request factory methods for these child mappings.
    private static CustomerAddress CreateAddress(CustomerAddressRequest x) => CustomerAddress.Create(x);
    private static BankAccount CreateBank(BankAccountRequest x) => BankAccount.Create(x);
    private static CustomerDocumentReference CreateDocument(CustomerDocumentReferenceRequest x) => CustomerDocumentReference.Create(x);
    private static CustomerTrackEntry CreateTrack(CustomerTrackRequest x) => CustomerTrackEntry.Create(x);
    private static FactoringSubLimit CreateFactoring(FactoringSubLimitRequest x) => FactoringSubLimit.Create(x);
}
```

If the repository uses a different existing response mapper name, retain that existing mapper and replace only the `CustomerResponseMapper.Map` call with its exact method. Do not introduce persisted display names to make the mapper simpler.

## Task 7 — CREATE `Finnova.Service/Customers/CustomerCodeGenerator.cs`

Create the adapter below using the existing `INumberingSchemeRepository` method and formatter signature in the repository project. If the formatter method has a different existing name, use that existing method without changing the numbering boundary.

```csharp
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Customers.Validation;

namespace Finnova.Service.Customers;

public sealed class CustomerCodeGenerator(INumberingSchemeRepository numberingSchemes)
    : IDocumentNumberGenerator
{
    public async Task<string> GenerateAsync(
        string scope,
        int length,
        CancellationToken ct = default)
    {
        var raw = await numberingSchemes.IssueNextAsync("CUSTOMER", null, ct);
        if (string.IsNullOrWhiteSpace(raw))
            throw new CustomerConditionalApplicabilityException(
                new[] { "CustomerCode could not be generated." });

        var code = raw.Trim();
        CustomerCodeRules.ValidateGenerated(code);
        if (code.Length != length)
            throw new ArgumentException(
                "The generated CustomerCode has an invalid length.", nameof(length));
        return code;
    }
}
```

Use the project’s existing safe Customer exception for a missing numbering result if one already exists. Do not use a GUID, request field, static counter, or test-only implementation in production.

## Task 8 — MODIFY `Finnova.Service/Customers/Queries/QueryCustomer.cs`

Ensure the query handler awaits the projection mapper and passes the cancellation token. Replace the synchronous projection call with this exact pattern in the handler’s query path:

```csharp
public async Task<IReadOnlyCollection<CustomerResponse>> Handle(
    QueryCustomerQuery request,
    CancellationToken cancellationToken)
{
    var customers = await repository.SearchAsync(request, cancellationToken);
    var responses = new List<CustomerResponse>(customers.Count);

    foreach (var customer in customers)
        responses.Add(await projection.ProjectAsync(customer, cancellationToken));

    return responses;
}
```

Adapt only the repository method and response collection type names if the existing query contract differs. The essential behavior is asynchronous current-master projection; do not call `.Result`, `.Wait()`, or a stale stored-name property.

## Task 9 — CREATE/register Customer application adapters

Create or retain the existing `AccountContractBoundary` and `CustomerChildMapper` implementations in `Finnova.Service/Customers/CustomerChildAdapters.cs`. Register all application ports in `Finnova.SystemAdminService/Program.cs` immediately after repository/service registration:

```csharp
builder.Services.AddScoped<IDocumentNumberGenerator, CustomerCodeGenerator>();
builder.Services.AddScoped<ICustomerAggregateMapper, CustomerAggregateMapper>();
builder.Services.AddScoped<ICustomerProjectionMapper, CustomerProjectionMapper>();
builder.Services.AddScoped<ICustomerChildMapper, CustomerChildMapper>();
builder.Services.AddScoped<IAccountContractBoundary, AccountContractBoundary>();
```

Add the required import:

```csharp
using Finnova.Service.Customers;
```

Do not register test fakes in production. The integration fixture overrides only `IIndiaAddressControl`.

## Task 10 — MODIFY `Finnova.Tests/Integration/SystemAdminAppFactory.cs`

Inside the test-host service configuration, preserve the existing EF Core InMemory replacement and add the provider warning configuration. Use the existing database name/options convention:

```csharp
services.AddDbContext<FinnovaDbContext>(options =>
{
    options.UseInMemoryDatabase(databaseName);
    options.ConfigureWarnings(warnings =>
        warnings.Ignore(Microsoft.EntityFrameworkCore.InMemoryEventId.TransactionIgnoredWarning));
});
```

Seed an idempotent global English `CUSTOMER` numbering scheme with the existing numbering entity properties:

```csharp
if (!db.NumberingSchemes.Any(x => x.DocumentType == "CUSTOMER" && x.CompanyId == null))
{
    db.NumberingSchemes.Add(new NumberingScheme
    {
        DocumentType = "CUSTOMER",
        CompanyId = null,
        Format = "CUST{SEQ:8}",
        CurrentNumber = 0,
        IsActive = true,
    });
}
```

Match the exact property names already used by the repository entity. Do not add non-English values or a production fallback counter. Ensure the resulting formatted value is exactly 12 alphanumeric characters.

## Task 11 — MODIFY `Finnova.Tests/Integration/CustomerOnboardingApiTests.cs`

Retain the existing eight targeted tests and ensure the default-bank failure asserts the middleware’s current contract:

```csharp
await AssertCustomerProblemAsync(
    response,
    HttpStatusCode.Conflict,
    "ERR-CUSTOMER-DEFAULT-BANK");
```

Retain these behavioral assertions:

```csharp
Assert.Equal(12, created!.CustomerCode.Length);
Assert.True(created.CustomerCode.All(char.IsLetterOrDigit));
Assert.Equal(created.CustomerCode, modified!.CustomerCode);
Assert.Equal("Renamed Group", projection.GroupName);
Assert.DoesNotContain(
    "GroupName",
    typeof(Customer).GetProperties().Select(x => x.Name),
    StringComparer.Ordinal);
```

The fixture must continue to use `CustomerOnboardingAppFactory` and its deterministic `IIndiaAddressControl` replacement. Do not weaken authorization or replace repository-backed master validation with a unit fake.

## Task 12 — CREATE focused contract tests

Create `Finnova.Tests/Unit/CustomerOnboardingContractTests.cs` with the following complete content. It protects the two reported interface contracts and the review decision without exposing a new API behavior:

```csharp
using Finnova.Models.Contracts.Customers;
using Finnova.Service.Customers.Commands;
using Finnova.Service.Customers.Validation;
using Xunit;

namespace Finnova.Tests.Unit;

public sealed class CustomerOnboardingContractTests
{
    [Fact]
    public void Clear_default_command_is_a_no_result_request()
    {
        var command = new ClearDefaultBankAccountCommand("CUST00000001", "system-admin");
        Assert.IsAssignableFrom<MediatR.IRequest>(command);
        Assert.IsNotType<MediatR.IRequest<MediatR.Unit>>(command);
    }

    [Fact]
    public void Non_empty_bank_collection_still_requires_one_default()
    {
        var account = new BankAccountRequest(
            null, "1234567890", "BANK", null, null, null, null, null, false);

        Assert.ThrowsAny<Exception>(() => BankAccountRules.Validate(new[] { account }));
    }
}
```

Keep the existing `InMemoryNationalityRepository` tests or add a fact that inactive rows return null and active codes match after trimming/case normalization.

## Task 13 — Static review and later verification

Before running commands, inspect the diff and confirm:

- No source file under the frontend repository changed.
- No `bin/` or `obj/` file changed.
- No persisted Customer master-name field was added.
- No public clear-default route was added.
- `ClearDefaultBankAccountCommand` still returns `Task` and remains subject to the current invariant.
- `GetActiveByCodeAsync` exists on the in-memory nationality fake.
- The SystemAdmin DI graph contains every Customer application port.

After the user applies the tasks, run the following from `e:\Finnova\Finnova-API` and record exact output in the task evidence/plan:

```powershell
dotnet test Finnova.Tests\Finnova.Tests.csproj --filter FullyQualifiedName~CustomerOnboardingApiTests --no-restore
dotnet build Finnova.Tests\Finnova.Tests.csproj --no-restore
dotnet test Finnova.Tests\Finnova.Tests.csproj --no-restore
dotnet build Finnova.Backend.slnx --no-restore
```

The narrow command is the acceptance gate. Full-project failures unrelated to this class must be recorded with their test names and not hidden. Generated build output is not a deliverable.

## Commit message template

Use a short conventional subject followed by evidence only after implementation and verification have actually been performed:

```text
fix: resolve CustomerOnboardingApiTests integration wiring

Targeted: dotnet test Finnova.Tests\Finnova.Tests.csproj --filter FullyQualifiedName~CustomerOnboardingApiTests --no-restore => <result>
Build: dotnet build Finnova.Tests\Finnova.Tests.csproj --no-restore => <result>
Full tests: dotnet test Finnova.Tests\Finnova.Tests.csproj --no-restore => <result>
Solution build: dotnet build Finnova.Backend.slnx --no-restore => <result>
Remaining blockers: <none or exact environmental/test names>
```
