# Customer Master Spec Investigation and Design Inputs

## Investigation scope and evidence

This report is the design input for the three-document customer-master spec. Inspection covered both repositories and did not modify application source, run a build/test command, or run a migration.

- Backend: `e:\Finnova\Finnova-API`
- Frontend: `E:\Finnova\Finnova-UI\Finnova-UI`
- Target spec directory: `e:\Finnova\Finnova-API\.kiro\specs\customer-master`
- Report directory: `e:\Finnova\Finnova-API\.agents\tasks\customer-master-spec`

The current target directory was empty when inspected. No customer task note was present under `.agents/tasks`; the setup inspection also confirmed that no prior customer-master spec artifact had been created there. The existing customer-related material is instead under:

- `e:\Finnova\Finnova-API\.kiro\specs\customer-master-reference-mind-mappings\`
- `e:\Finnova\Finnova-API\.kiro\specs\customer-document-blob-upload\`
- `e:\Finnova\Finnova-API\.kiro\specs\fix-customer-onboarding-api-tests\`

The reference spec directory names are `nationality-master-management` and `organization-hierarchy-master-management`, not `nationality` or `organization-hierarchy`. `nationality-master-management` contains all four reference files. `organization-hierarchy-master-management` contains `requirements.md`, `design.md`, and `.config.kiro`, but no `tasks.md`; that missing file is an open documentation gap, not something to invent.

The existing Customer material is broader than a simple CRUD master. It calls the feature **Customer Onboarding** and treats Customer Master as earlier terminology. The new requirements/design/tasks documents must state whether the requested “customer master” means this existing Customer Onboarding aggregate or a narrower master-data screen. Until confirmed, the current Customer Onboarding slice is the only concrete customer implementation found.

## Current customer-master state

### Present backend domain model

The aggregate and owned records already exist in `Finnova.Models\Domain\Entities`:

- `Customer.cs`
- `CustomerAddress.cs`
- `IndividualCustomerDetails.cs`
- `NonIndividualCustomerDetails.cs`
- `BankAccount.cs`
- `CustomerDocumentReference.cs`
- `CustomerTrackEntry.cs`
- `CustomerCreditDetails.cs`
- `FactoringSubLimit.cs`
- `CustomerAuditEntry.cs`

`Customer` is a `sealed class` with private setters. Its concrete persisted scalar properties are:

- `Guid Id` initialized with `Guid.NewGuid()`.
- `string CustomerCode`, generated, exactly 12 characters by the service, and intended to be immutable.
- `Guid CompanyId`.
- `bool IsCustomer`, `bool IsGuarantor`, and `bool IsCoApplicant`.
- `string CustomerTypeCode`.
- Optional reference codes: `GroupCode`, `IndustryCode`, `CustomerPostingGroupCode`, `ConstitutionCode`, `NationalityCode`, `TitleCode`, `MaritalStatusCode`, `CustomerBusinessTypeCode`, `CustomerBusinessSubTypeCode`, `IndustrySectorCode`, and `SubSectorCode`.
- Name/value fields: `FirstName` through `SixthName`, `Notes`, `FinancialRequirement`, `Accommodation`, `RelationCode`, and `EmployerEconomicActivityCode`.
- Nullable flags `IsVip`, `IsSeniorMember`, and `IsRelatedParty`.
- `CustomerLifecycleStatus Status`, `DateTime CreatedAtUtc`, and `DateTime UpdatedAtUtc`.
- Owned collections/properties: `List<CustomerAddress> Addresses`, `IndividualCustomerDetails? IndividualDetails`, `NonIndividualCustomerDetails? NonIndividualDetails`, `List<BankAccount> BankAccounts`, `List<CustomerDocumentReference> Documents`, `List<CustomerTrackEntry> Tracks`, and `List<FactoringSubLimit> FactoringSubLimits`.

The aggregate currently exposes `SetGeneratedCode(string code)`, `Initialize(...)`, `SetIndividualDetails(...)`, `SetNonIndividualDetails(...)`, and `Touch()`.  It does not persist display-name fields such as `GroupName`, `IndustryName`, `ConstitutionName`, `NationalityName`, or `GLName`. Those names are response-projection fields only.

The current enums under `Finnova.Models\Domain\Enums` are:

- `CustomerLifecycleStatus`: `Active = 1`, `Inactive = 2`.
- `CustomerAddressType`: `Communication = 1`, `Permanent = 2`.
- `CustomerTrackStatus`: `Blacklisted = 1`, `Released = 2`.
- `CustomerAuditAction`: `Created = 1`, `Modified = 2`, `BankChanged = 3`, `DocumentChanged = 4`, `TrackChanged = 5`, `CreditChanged = 6`, and `FactoringChanged = 7`.

No enums exist for Customer Type, Group, Industry, Constitution, Title, Marital Status, Business Type/Sub-Type, Sector/Sub-Sector, Track Type, Nationality, GL, or Document values. Those are string codes resolved through master-data boundaries.

`CustomerAuditEntry.cs` is immutable by API shape: it has private setters for `Id`, `CustomerId`, `Action`, `ActorId`, `ChangedFieldsJson`, and `CreatedAtUtc`, and a static factory:

```csharp
CustomerAuditEntry.Create(Guid customerId, CustomerAuditAction action, string actorId, string changedFieldsJson)
```

The audit snapshot currently stores safe JSON rather than a strongly typed before/after field set.

### Present backend contracts

`Finnova.Models\Contracts\Customers\CustomerContracts.cs` contains the existing request and response records. Concrete request types include:

- `CustomerAddressRequest` with `CustomerAddressType AddressType` and Indian-address/contact fields such as `PostalCode`, `City`, `State`, `Country`, `Mobile`, `Email`, and `Website`.
- `IndividualCustomerDetailsRequest` with `GenderCode`, `DateOfBirth`, `IndiaIdentityType`, `IndiaIdentityReference`, `IdentityIssueDate`, and `IdentityExpiryDate` among other details.
- `NonIndividualCustomerDetailsRequest` with business/profile, company, director, financial, industry, CR, factoring, and covenant fields.
- `BankAccountRequest` with `AccountTypeCode`, `AccountNumber`, `BankCode`, `MICRCode`, `ClearingDate`, `Address`, `ContractNumber`, `SubContractNumber`, and `IsDefault`.
- `CustomerDocumentReferenceRequest` with `ConstitutionCode`, `DocumentDefinitionId`, `DocumentCode`, collection flags, `Value`, `ValueSource`, `DmsReference`, and `ViewMetadata`.
- `CustomerTrackRequest` with `LineOfBusinessCode`, `AccountNumber`, `TrackTypeCode`, `TrackDate`, `Reason`, and `ByLogin`.
- `FactoringSubLimitRequest` with factoring and covenant fields.
- `CustomerCreateRequest`, which requires `CompanyId`, three role flags, `CustomerTypeCode`, all create-time reference/name fields, and collections for addresses, individual/non-individual details, banks, documents, tracks, and factoring sub-limits.
- `CustomerModifyRequest`, which requires `CustomerCode` and `CompanyId`, carries editable reference/detail/child fields, and has `CopyCommunicationAddressToPermanent`.
- `CustomerQueryRequest(string? CustomerCode, string? Search, Guid? CompanyId, int Page = 1, int PageSize = 25)`.

Response records include `CustomerResponse`, `CustomerAddressResponse`, `BankAccountResponse`, `CustomerDocumentResponse`, `CustomerTrackResponse`, `CustomerCreditResponse`, and `FactoringSubLimitResponse`. `CustomerResponse` contains persisted/reference values plus projection values such as `GroupName`, `IndustryName`, `GLName`, `ConstitutionName`, and `NationalityName`. `CustomerResponse` also returns `CreatedAtUtc`, `UpdatedAtUtc`, child collections, and the `CustomerLifecycleStatus`.

The contract file also defines `LookupCatalogEntry`, `CustomerLookupCatalogResponse`, `CustomerCodeLookup`, `CopyCommunicationAddressRequest`, `CopyCommunicationAddressResult`, `CustomerApplicabilityContext`, `CustomerFieldState`, `CustomerFieldApplicability`, and `MasterReferenceValidationResult`.

The important boundary rule is already visible in the contract split: write requests contain reference codes/IDs, while current master display names appear only in the response projection. The spec must preserve this and must not add bilingual or language-suffixed fields.

### Present backend exceptions and error family

`Finnova.Models\Domain\Exceptions\CustomerExceptions.cs` already defines:

- `CustomerErrorCodes.Family = "ERR-CUSTOMER"`.
- `Validation`, `NotFound`, `Conflict`, `Unexpected`, and `BoundaryUnavailable` codes.
- Reference codes: `LookupReference`, `NationalityReference`, `GLReference`, and `DocumentReference`.
- `Immutable`, `DuplicateReference`, `Duplicate`, and `DefaultBank` codes.
- `CustomerDomainException` with an `ErrorCode` property.
- `CustomerNotFoundException`.
- `CustomerCodeConflictException`.
- `CustomerImmutableFieldException`.
- `CustomerLookupReferenceNotFoundException`.
- `CustomerNationalityReferenceNotFoundException`.
- `CustomerGLPostingGroupInvalidException`.
- `CustomerDocumentDefinitionUnavailableException`.
- `CustomerAddressControlValidationException`.
- `CustomerConditionalApplicabilityException`.
- `CustomerDuplicateReferenceException`.
- `CustomerDuplicateIdentityException`.
- `CustomerDuplicateBankAccountException`.
- `CustomerDefaultBankAccountException`.
- `CustomerChildRecordNotFoundException`.
- `CustomerBoundaryUnavailableException`.

Exception messages are designed to avoid raw document content, secrets, tokens, SQL, and unnecessary PII. A new spec should extend this existing family only where the confirmed customer ticket needs behavior that is not already represented.

### Present EF Core model and configuration

`Finnova.Repository\Context\FinnovaDbContext.cs` already exposes these Customer sets:

```csharp
public DbSet<Customer> Customers => Set<Customer>();
public DbSet<CustomerAddress> CustomerAddresses => Set<CustomerAddress>();
public DbSet<IndividualCustomerDetails> IndividualCustomerDetails => Set<IndividualCustomerDetails>();
public DbSet<NonIndividualCustomerDetails> NonIndividualCustomerDetails => Set<NonIndividualCustomerDetails>();
public DbSet<BankAccount> CustomerBankAccounts => Set<BankAccount>();
public DbSet<CustomerDocumentReference> CustomerDocumentReferences => Set<CustomerDocumentReference>();
public DbSet<CustomerTrackEntry> CustomerTrackEntries => Set<CustomerTrackEntry>();
public DbSet<FactoringSubLimit> CustomerFactoringSubLimits => Set<FactoringSubLimit>();
public DbSet<CustomerAuditEntry> CustomerAuditEntries => Set<CustomerAuditEntry>();
```

`OnModelCreating` calls `modelBuilder.ApplyConfigurationsFromAssembly(typeof(FinnovaDbContext).Assembly)`, so the existing configuration classes are auto-discovered. The relevant paths and concrete schema conventions are:

- `Finnova.Repository\Configuration\CustomerConfiguration.cs`: table `Customers`; unique `CustomerCode` index; `CustomerTypeCode` required max 50; `GroupCode` max 4; `IndustryCode` max 3; reference codes mostly max 50; name parts max 100; `Notes` max 1000; financial/accommodation max 240; `Status` stored as string max 30; composite index `(CompanyId, NationalityCode)`; no persisted master display names.
- `CustomerAddressConfiguration.cs`: table `CustomerAddresses`; `AddressType` stored as string; most address/contact fields max 300; `PostalCode` max 6; unique `(CustomerId, AddressType)` index.
- `IndividualCustomerDetailsConfiguration.cs`: table `IndividualCustomerDetails`; unique `CustomerId`; lengths for `GenderCode`, `SpouseName`, `Qualification`, `Profession`, `IndiaIdentityType`, and `IndiaIdentityReference`.
- `NonIndividualCustomerDetailsConfiguration.cs`: table `NonIndividualCustomerDetails`; unique `CustomerId`; concrete bounds including `BusinessProfile` 240, `GeographicalCoverage` 120, `JVPartnerName` 120, `CEOOrMDName` 50, `ResidentialAddress` 300, and `BusinessActivities` 100.
- `BankAccountConfiguration.cs`: table `CustomerBankAccounts`; `AccountNumber` required max 16; `NormalizedDuplicateKey` required max 400; `MICRCode` max 10; `Address` max 300; contract fields max 15; unique `(CustomerId, NormalizedDuplicateKey)` and filtered unique `(CustomerId)` where `[IsDefault] = 1`.
- `CustomerDocumentReferenceConfiguration.cs`: table `CustomerDocumentReferences`; definition/reference metadata is stored, but raw document bytes are not a Customer field.
- `CustomerTrackEntryConfiguration.cs`: table `CustomerTrackEntries`; `TrackTypeCode` required max 50, account and reason fields bounded, and unique `(CustomerId, AccountNumber, TrackTypeCode)`.
- `FactoringSubLimitConfiguration.cs`: table `CustomerFactoringSubLimits`; factoring type/covenant fields are bounded.
- `CustomerAuditEntryConfiguration.cs`: table `CustomerAuditEntries`; `ActorId` max 100, `ChangedFieldsJson` max 8000, string enum action max 40, indexes `(CustomerId, CreatedAtUtc)` and `ActorId`.

No Customer-specific EF migration was found. The only migration file found under `Finnova.Repository\Migrations` is the Asset migration `20261010161544_AddAssetMigrations.cs` plus its designer. This is a material implementation dependency: the final design/tasks must either add the Customer migration step if the database is not already provisioned, or explicitly record that schema deployment is handled outside this ticket.

### Present repositories and DI

`Finnova.Repository\Interfaces\ICustomerRepository.cs` defines separate contracts:

```csharp
public interface ICustomerRepository
{
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
    Task<(IReadOnlyCollection<Customer> Items, int Total)> SearchAsync(CustomerQueryRequest request, CancellationToken ct = default);
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

The concrete implementations are:

- `Finnova.Repository\Repositories\CustomerRepository.cs`: includes `Addresses`, `IndividualDetails`, `NonIndividualDetails`, `BankAccounts`, `Documents`, `Tracks`, and `FactoringSubLimits`; trims CustomerCode; checks identity via `IndividualDetails.IndiaIdentityReference`; stages `AddAsync` without saving so the feature unit of work can commit the aggregate and audit atomically.
- `CustomerReadRepository.cs`: includes the same children; filters by `CompanyId`, exact `CustomerCode`, or substring search across `CustomerCode`, `FirstName`, and `SecondName`; counts before paging; orders by `CustomerCode`; currently clamps invalid page values to 1/25 instead of rejecting them.
- `CustomerAuditRepository.cs`: stages `CustomerAuditEntry` using `AddAsync` without saving.
- `CustomerUnitOfWork.cs`: wraps `FinnovaDbContext` transactions and saves the aggregate/audit changes together.
- `RepositoryBase<T>` remains the generic CRUD base, and its default `AddAsync`, `UpdateAsync`, and `DeleteAsync` call `SaveChangesAsync`; Customer repositories deliberately hide/stage where atomicity requires it.

`Finnova.Repository\DependencyInjection.cs` already registers `ICustomerRepository`, `ICustomerAuditRepository`, `ICustomerReadRepository`, and `ICustomerUnitOfWork` as scoped services. It also registers the existing `INationalityRepository`, `INationalityAuditRepository`, `ILookupRepository`, `IGLAccountRepository`, and `IDocumentRepository` boundaries used by Customer validation/projection.

### Present CQRS/service/application slice

Customer application code is under `Finnova.Service\Customers`:

- `CustomerApplicationPorts.cs` defines `IDocumentNumberGenerator`, `IAccountContractBoundary`, `ICustomerAggregateMapper`, `ICustomerProjectionMapper`, and `ICustomerChildMapper`.
- `CustomerCodeGenerator.cs` calls `INumberingSchemeRepository.IssueNextAsync("CUSTOMER", NumberScope.Global, null, DateTime.UtcNow, ct)`, formats through `NumberFormatter.Format(...)`, and enforces a 12-character alphanumeric result. This is the concrete numbering shape to reuse; do not copy the older task snippet that assumes a different `IssueNextAsync` signature.
- `CustomerAggregateMapper.cs` creates and modifies the aggregate, maps approved references and child collections, creates `CustomerAuditEntry` snapshots, and creates `CustomerResponse` projections from resolved references.
- `CustomerProjectionMapper.cs` resolves current Lookup, Nationality, and GL names via `ILookupReferenceResolver`, `INationalityReferenceResolver`, and `IGLPostingGroupResolver`; it does not mutate the aggregate.
- `CustomerChildAdapters.cs` contains `AccountContractBoundary` and child mapping adapters.
- `Customers\References\CustomerMasterReferenceValidator.cs` resolves active Lookup values, active Nationality, GL scope, applicable Document definitions, India Address Control results, mandatory documents, and applicability rules before mutation.
- `LookupReferenceResolver.cs`, `NationalityReferenceResolver.cs`, `GLPostingGroupResolver.cs`, `DocumentDefinitionResolver.cs`, `IndiaAddressControl.cs`, `ReferenceProjections.cs`, and `CustomerLookupTypes.cs` define the adapter boundaries and constants.
- `Customers\Validation\CustomerRules.cs` defines `CustomerCodeRules`, `CustomerFieldRules`, `BankAccountRules`, `DocumentRules`, `DuplicateIdentityRules`, and `CustomerPrerequisiteRules`.
- `Customers\Commands\CreateCustomerCommand.cs`: validates company/type/basic request shape, Customer field rules, bank invariant, references, CustomerCode, identity uniqueness, then writes Customer plus a `Created` audit in a transaction.
- `ModifyCustomerCommand.cs`: loads by CustomerCode, enforces immutable code and Company scope, validates references/children, applies changes, writes a `Modified` audit, and commits transactionally.
- `CustomerChildCommands.cs` and `CustomerChildCommandHandler.cs`: cover bank, document, track, credit, factoring, address-copy, and default-bank operations, with `CustomerAuditAction` audit entries where applicable.
- `Customers\Queries\QueryCustomer.cs`: sends `QueryCustomer(CustomerQueryRequest Request)` and returns `IReadOnlyCollection<CustomerResponse>`, not the `PaginatedResponse<T>` shape used by the master-data reference specs.

`Finnova.SystemAdminService\Program.cs` already registers the Customer ports/adapters, repository DI, MediatR, FluentValidation, JWT Bearer, and the `SystemAdmin` policy. It currently registers `CustomerFieldApplicabilityEvaluator`, `CustomerMasterReferenceValidator`, `CustomerCodeGenerator`, the aggregate/projection/child mappers, `AccountContractBoundary`, and the existing `IIndiaAddressControl` adapter.

### Present API, middleware, and gateway

`Finnova.SystemAdminService\Controllers\CustomerOnboardingController.cs` is an `[ApiController, Authorize(Policy = "SystemAdmin"), Route("api/customer-onboarding")]`. It currently exposes:

- `POST /api/customer-onboarding` → `Create(CustomerCreateRequest)`.
- `PUT /api/customer-onboarding/{customerCode}` → `Modify(CustomerModifyRequest)`.
- `GET /api/customer-onboarding` → `Query(CustomerQueryRequest)`.
- `POST /api/customer-onboarding/{customerCode}/banks`.
- `POST /api/customer-onboarding/{customerCode}/banks/{bankId}/default`.
- `POST /api/customer-onboarding/{customerCode}/documents`.
- `POST /api/customer-onboarding/{customerCode}/tracks`.
- `POST /api/customer-onboarding/{customerCode}/addresses/copy-communication`.
- `PATCH /api/customer-onboarding/{customerCode}/credit/valid-up-to`.
- `POST /api/customer-onboarding/{customerCode}/factoring-sub-limits`.

There is no catalog action in this controller even though the frontend service interface has `loadCatalog(companyId: string)`. A search of the current backend service/controller code did not find `CustomerCatalog`, `GetCatalog`, or `catalog`. This mismatch must be resolved in the final spec rather than silently assuming the endpoint exists.

`Finnova.SystemAdminService\Middleware\ExceptionHandlingMiddleware.cs` already has a typed Customer branch. It maps `CustomerNotFoundException` and `CustomerChildRecordNotFoundException` to 404, Customer code/immutable/duplicate/default-bank conflicts to 409, master/address/applicability errors to 400, and boundary-unavailable to 503. It selects `ERR-CUSTOMER-400` and `ERR-CUSTOMER-500` for generic validation/fallback based on the `/api/customer-onboarding` path. The existing ProblemDetails shape is `Status`, `Title = code`, `Detail`, and `Extensions["code"] = code`. The frontend currently reads `detail` in CustomerOnboarding itself, while the shared axios interceptor reads `error.response.data.message`; the final spec must reconcile this response-field inconsistency.

`Finnova.ApiGateway\appsettings.json` already contains:

- `CustomerOnboardingAliasRoute`: `/api/ua/api/customer-onboarding/{**catch-all}` to `systemadmin-cluster` using `PathPattern` `/api/customer-onboarding/{**catch-all}`.
- `ua-route`: `/api/ua/{**catch-all}` to `ua-cluster`.
- `systemadmin-route`: `/api/systemadmin/{**catch-all}` to `systemadmin-cluster`.
- Similar alias pairs for Lookup, Nationality, Organization Hierarchy, DCN, Court, Entity, Drawee Bank, User, Asset, GL, and Document.

Customer should remain on `systemadmin-cluster`; do not add a new host or collapse hosts. Any new UI route must either reuse `CustomerOnboardingAliasRoute` or add an explicitly justified alias for the exact UI-composed path.

## Backend testing evidence and conventions

The test project uses xUnit, FsCheck.Xunit, Moq, EF Core InMemory, and `WebApplicationFactory`.

- `Finnova.Tests\Integration\SystemAdminAppFactory.cs` derives from `WebApplicationFactory<Finnova.SystemAdminService.Controllers.CourtController>`, replaces SQL Server EF registrations with InMemory, ignores `InMemoryEventId.TransactionIgnoredWarning`, seeds English `LookupValue` rows, and seeds a global `CUSTOMER` `NumberingScheme` with `FormatTemplate = "CUST{SEQ:8}"`, `SeqPadding = 8`, `Scope = NumberScope.Global`, and `NumberResetRule.Never`.
- `Finnova.Tests\Integration\CustomerOnboardingApiTests.cs` derives from a `CustomerOnboardingAppFactory`, uses `DevJwt.SystemAdmin()`, seeds Lookup/Nationality/GL/Document rows through `FinnovaDbContext`, and covers unauthorized create, reference failures, generated immutable code, atomic rollback, projection name refresh, and unknown modify.
- `Finnova.Tests\Unit\CustomerOnboardingRulesTests.cs` uses xUnit facts and FsCheck `[Property(MaxTest = 100)]` for CustomerCode, identity normalization, amount/percentage/date/PIN rules, bank default invariants, and display-name contract shape.
- `Finnova.Tests\Unit\CustomerMasterReferenceValidatorTests.cs` uses Moq to verify active Lookup, Nationality, Document, and test-address boundary behavior.
- `Finnova.Tests\Infrastructure\InMemoryNationalityRepository.cs` and `InMemoryNationalityAuditRepository.cs` show the hand-written repository-double style. The nationality fake explicitly trims and compares codes with `StringComparison.OrdinalIgnoreCase`, filters inactive rows in `GetActiveByCodeAsync`, returns clones, and mirrors pagination/order behavior.
- Existing property files under `Finnova.Tests\Properties` use FsCheck.Xunit and a minimum of 100 iterations. The nationality reference design uses 13 named correctness properties, one property test each, and separate xUnit edge/integration tests.

The final customer tasks should add or update focused properties only for pure customer logic and use plain xUnit/Moq/InMemory facts for handlers and contract edges. Host wiring, authorization, and end-to-end create/modify/query flows belong in `WebApplicationFactory<Program>` tests. Do not treat a grep/static check as verification; later implementation tasks must use the actual documented build/test commands.

## Frontend patterns and current customer UI state

### Confirmed stack and commands

`E:\Finnova\Finnova-UI\Finnova-UI\package.json` confirms:

- React `18.3.1`, TypeScript `5.5.4`, Vite `5.4.2`.
- MUI `@mui/material` `6.1.0`, `@mui/x-data-grid` `7.18.0`, and `@mui/icons-material`.
- Axios, React Router v6, Vitest `2.1.9`, jsdom, React Testing Library, and user-event.
- `npm run build` runs `tsc -b && vite build`.
- `npm test` runs `vitest run`.

`vite.config.ts` uses `environment: 'jsdom'`, `globals: true`, `setupFiles: './src/test/setup.ts'`, and a 20-second test timeout. There is no Redux or RxJS dependency.

### Shared HTTP/service conventions

`src/services/api.ts` creates the single shared Axios instance. Its base URL is `import.meta.env.VITE_API_BASE_URL || '/api'`, it injects `Bearer ${localStorage.getItem('finnova_token')}`, and its response interceptor derives messages from `error.response.data.message` before showing a toast. It handles timeout, network, 400/401/403/404/409/422/429/500/502/503, clears auth on 401, and redirects to `/` when required. All real feature services must use this instance; no ad-hoc Axios client is allowed.

The established master-data service shape is visible in:

- `src/services/interfaces/lookup.interface.ts` and `nationality.interface.ts` for feature interfaces and `PaginatedResponse<T>`.
- `src/services/real/lookup.real.ts` and `nationality.real.ts` for Axios DTO mapping and service-relative paths.
- `src/services/mock/lookup.mock.ts` and `nationality.mock.ts` for in-memory English-only seed data and backend-shaped errors.
- `src/services/lookup.service.ts` and `nationality.service.ts` for `import.meta.env.VITE_USE_MOCK_API === 'true'` selection.
- `src/services/interfaces/index.ts`, `src/models/index.ts`, and `src/services/index.ts` for barrels.

`src/services/interfaces/customerOnboarding.service.ts` is different from the master pattern. It currently exports a non-prefixed `CustomerOnboardingService` interface with:

```typescript
loadCatalog(companyId: string): Promise<CustomerCatalog>;
create(request: CustomerForm): Promise<CustomerProjection>;
modify(customerCode: string, request: CustomerForm): Promise<CustomerProjection>;
query(search?: string): Promise<CustomerProjection[]>;
copyCommunicationAddress(customerCode: string): Promise<CustomerProjection>;
```

It is not exported from `src/services/interfaces/index.ts`. The selector `src/services/customerOnboarding.service.ts` chooses between `src/services/real/customerOnboarding.service.ts` and `src/services/mock/customerOnboarding.service.ts`, so it does not mirror the `feature.interface.ts` / `feature.real.ts` / `feature.mock.ts` naming exactly.

The real Customer service uses the shared `api` instance and calls:

- `GET /customer-onboarding/catalog` with `companyId`.
- `POST /customer-onboarding`.
- `PUT /customer-onboarding/{encodeURIComponent(customerCode)}`.
- `GET /customer-onboarding` with `search` and returns `data.items ?? data`.
- `POST /customer-onboarding/{customerCode}/addresses/copy-communication`.

The mock Customer service stores a module-level `CustomerProjection[]`, seeds English `Single` and `India` lookup values, creates the fixed code `MOCK00000001`, supports modify/query/copy-address, but does not shape API errors like the real backend and does not model the full backend pagination contract.

### Current Customer UI model/page/tests

`src/models/customerOnboarding.ts` defines:

- `CustomerOnboardingMode = 'create' | 'modify' | 'query'`.
- Sections `basic-address`, `individual`, `non-individual`, `bank`, `constitution-documents`, `track`, `credit`, and `factoring`.
- `CustomerAddress`, `CustomerForm`, `BankAccountForm`, `DocumentReferenceForm`, `TrackForm`, `CreditDetails`, `FactoringForm`, `CustomerProjection`, and `CustomerCatalog`.
- The model uses one plain field per display concept and no bilingual/language-suffixed fields.

`src/pages/CustomerOnboarding/CustomerOnboarding.tsx` uses React hooks, MUI `Tabs`, `Tab`, `Alert`, `CircularProgress`, and `Button`. It loads an existing projection when `customerCode` exists, saves only outside query mode, and renders `CustomerOnboardingTabs`. Its current catch blocks read `e?.response?.data?.detail ?? e.message`, not the shared interceptor's `message` field.

`src/pages/CustomerOnboarding/components/CustomerOnboardingTabs.tsx` dispatches to eight tab components: `BasicAddressTab`, `IndividualDetailsTab`, `NonIndividualDetailsTab`, `BankDetailsTab`, `ConstitutionDocumentsTab`, `CustomerTrackTab`, `CustomerCreditDetailsTab`, and `FactoringSubLimitTab`.

`src/pages/CustomerOnboarding/CustomerOnboarding.test.tsx` uses Vitest + RTL and mocks `../../services/customerOnboarding.service`. Its tests cover query-mode save gating, read-only fields, immutable Customer Code, reference-only request fields, projection names, deferred Customer Reference/Mind Mapping UI absence, query/save error rendering, and query-mode no-save behavior. The test factory uses English values such as `India`, `Retail`, and `Finance`.

`src/App.tsx` already wires:

```tsx
<Route path="/customer-onboarding" element={<CustomerOnboarding mode="create" />} />
<Route path="/customer-onboarding/:customerCode" element={<CustomerOnboarding mode="modify" />} />
<Route path="/customer-onboarding/:customerCode/view" element={<CustomerOnboarding mode="query" />} />
```

`src/components/Layout.tsx` already adds `Customer Onboarding` under the Administration group at `/customer-onboarding`. No new route or nav item is required for the existing page unless the confirmed ticket changes scope.

For the UI page shape, use `src/pages/NationalityMaster.tsx` and `src/pages/LookupMaster.tsx` as the master-screen references: hooks only, debounced search where applicable, MUI dialogs, DataGrid components, service calls, refresh-on-success, and leave current state unchanged on errors. For Customer’s multi-tab form, preserve the existing `CustomerOnboarding.tsx` and tab structure unless the ticket explicitly requires a new master grid or a distinct Customer Query screen.

## What is already implemented versus what the next spec must decide

### Already implemented or wired

- Customer aggregate, owned child entities, lifecycle/audit enums, and domain exceptions.
- Customer contracts for create, modify, query, child operations, projections, and master references.
- EF configurations and DbSets for Customer and child tables.
- Repository interfaces/implementations and transactional unit-of-work.
- CQRS create, modify, query, and child command handlers, validators, mappers, and reference adapters.
- SystemAdmin authorization, Customer controller routes, middleware error branch, and gateway alias.
- Backend unit/integration tests and in-memory test infrastructure.
- Frontend Customer form model, service interface/real/mock/toggle, page, tab components, routes, navigation, and Vitest/RTL coverage.

### Missing, inconsistent, or not verified

- No Customer-specific migration artifact was found under `Finnova.Repository\Migrations`.
- The requested customer-master ticket/source acceptance criteria were not present under `.agents\tasks`; the current concrete business contract is distributed across existing specs and code.
- The target `.kiro\specs\customer-master` documents do not exist yet.
- No backend `/api/customer-onboarding/catalog` action was found even though the UI interface/real service calls it.
- `CustomerQueryRequest` has paging fields, but `QueryCustomer` returns a collection and `CustomerReadRepository` clamps bad page input rather than validating and returning `PaginatedResponse<CustomerResponse>`.
- The current Customer frontend interface/service files are not exported through the common interface/model/service barrels in the same way as Lookup/Nationality.
- The current Customer mock uses a fixed ID/code and lacks backend-shaped conflict/validation errors; it should not silently be treated as a faithful real-service substitute.
- Middleware writes `ProblemDetails.Detail`, while `src/services/api.ts` primarily reads `data.message`, and the Customer page explicitly reads `data.detail`; the final error contract needs one confirmed response field or compatibility mapping.
- The customer code generator uses the real `INumberingSchemeRepository.IssueNextAsync` tuple signature and `NumberFormatter`; older customer task snippets in `customer-master-reference-mind-mappings` assume a different contract and must not be copied without adaptation.
- Existing aggregate mapper behavior and frontend request types should be checked against the supplied/confirmed FRS before adding fields. The existing requirements explicitly state that FINNOVA-51..55 source details were unavailable when prepared.
- Database migration/application strategy for Customer tables is not verified.

## Exact spec-document conventions to follow

### `requirements.md`

Mirror the structure of `nationality-master-management\requirements.md` and the current `customer-master-reference-mind-mappings\requirements.md`:

1. Start with `# Requirements Document`.
2. Add `## Introduction` describing the business capability, platform placement, scope, current terminology, and both repositories.
3. Add `## Glossary` with concrete terms such as Customer, Customer Code, Customer Mode, Customer Reference, master reference, projection, Customer Onboarding service/UI, audit entry, and any confirmed workflow terms.
4. Add `## Dependencies and Assumptions`, separating existing dependencies from explicit `Assumptions for Confirmation`.
5. Use numbered `## Requirements` with a user story and `#### Acceptance Criteria` for each requirement.
6. Write criteria in EARS form using `THE ... SHALL` for ubiquitous rules, `WHEN ... THE ... SHALL` for events, `WHILE ... THE ... SHALL` for state, `IF ... THEN ... SHALL` for unwanted events, and clearly labelled Optional/Complex cases when applicable.
7. Every silent decision must be called out inline as `**Assumption:**` or in a named assumptions list. Do not convert an unavailable FRS/story detail into an unlabelled requirement.
8. Preserve India-only, English-only, no-RTL, no Arabic/non-English sample data, single plain display fields, and no Redux/RxJS constraints.

### `design.md`

Mirror the broad architecture of `nationality-master-management\design.md` and the reusable-boundary discipline in `customer-master-reference-mind-mappings\design.md`:

1. Start with title, feature/source identifiers, and an overview.
2. State what is reused versus what is new/modified. Reuse existing Customer aggregate and adapters rather than describing a second parallel Customer model.
3. Include a requirements coverage map.
4. Explain the request flow and layer boundaries: UI → gateway → SystemAdmin host → auth/middleware → MediatR/validation → Customer references/handlers → repositories/unit of work → EF/SQL.
5. Include concrete data models, contracts, repository interfaces, CQRS commands/queries/validators/mappers, controller routes, middleware/error mapping, gateway routing, and frontend service/page/test interfaces.
6. Include error handling, security/privacy, atomicity, authorization, and migration/deployment assumptions.
7. Include design decisions/tradeoffs, especially the choice between extending the existing Customer Onboarding screen and creating a new Customer Master screen, query pagination shape, catalog endpoint ownership, migration status, and the ProblemDetails `message`/`detail` mismatch.
8. End with an implementation-ordered task list. It must be dependency-ordered and must point to the exact files the tasks document will create/modify.
9. Design explains shape and rationale; it must not be the only place where copy-paste code lives.

### `tasks.md`

Mirror the implementation ordering used by the Nationality tasks and the copy-paste discipline in the customer tasks:

1. Start with the feature title, scope, repositories, and execution rules.
2. Use one checked/unchecked task per implementation step in dependency order.
3. Mark every step `CREATE <path>` or `MODIFY <path>`.
4. For every CREATE, provide the complete file contents. For every MODIFY, show the exact replacement/insertion snippet and its location.
5. Use the real current signatures discovered above. In particular, use the existing CustomerCodeGenerator numbering tuple/formatter contract, `ICustomerRepository` in `Finnova.Repository.Interfaces`, and current controller/middleware/DI names.
6. Include backend entities/contracts/configuration/repositories/service/CQRS/controller/middleware/gateway/tests before dependent frontend steps, unless a confirmed change is UI-only.
7. Include exact migration command only if migration scope is confirmed. If migration generation is required, task it but do not claim it already exists.
8. Include frontend model/interface/real/mock/toggle/barrel/page/component/route/nav/test tasks only for confirmed UI changes. Reuse the existing CustomerOnboarding page/tab structure and `customerOnboarding.service.ts` pattern unless the confirmed ticket says to replace it.
9. End with implementation verification commands. The user-run convention says the spec itself must not run builds/tests/migrations; tasks may tell the user to run `dotnet build`, `dotnet test`, `npm run build`, `npm test`, and migration commands after copying the code.
10. Avoid hidden “optional” implementation branches. If a choice is unresolved, record it as an assumption and choose one explicit path in design/tasks after confirmation.

### `.config.kiro`

Create `e:\Finnova\Finnova-API\.kiro\specs\customer-master\.config.kiro` with a fresh GUID and exactly this configuration shape:

```json
{"specId":"<fresh-guid>","workflowType":"requirements-first","specType":"feature"}
```

## Silent decisions that MUST be recorded as ASSUMPTIONS in `requirements.md`

The following are not verified from an authoritative customer ticket/FRS in the current workspace and must not be silently decided in the final requirements:

1. **Terminology and scope:** whether “Customer Master” is exactly the existing “Customer Onboarding” feature, or whether a narrower master-data record/list is required; whether Customer Reference and Mind Mapping remain deferred.
2. **Authoritative business stories:** FINNOVA-51 through FINNOVA-55 and the supplied FRS are referenced by existing material, but their complete authoritative text was not available in the workspace. Exact field list, workflow, modes, tabs, lifecycle, and acceptance behavior require confirmation.
3. **Customer Code:** whether the current generated 12-character alphanumeric `CustomerCode` and the global `CUSTOMER` numbering scheme are authoritative; exact prefix/sequence/reset/scope, collision/retry handling, and whether code is immutable.
4. **Customer identity and uniqueness:** which India identity type/reference is required, normalization, duplicate scope, and whether duplicate identity rejection applies to all Customer types.
5. **Customer types/capacities:** allowed `CustomerTypeCode` values, whether `IsCustomer`, `IsGuarantor`, and `IsCoApplicant` combinations are valid, and whether a Customer may hold multiple capacity flags.
6. **Required versus optional fields:** requiredness and bounds for names, `CompanyId`, `CustomerTypeCode`, addresses, individual/non-individual details, bank rows, documents, tracks, factoring values, and every reference code.
7. **Lookup authority:** final module/type constants for `CustomerLookupTypes` and approved code/value sets; whether active-only validation is required for every reference on create, modify, and read.
8. **Nationality authority:** whether `NationalityCode` must resolve through active `INationalityRepository.GetActiveByCodeAsync`; whether nationality reads are limited to active rows and whether reference display-name projection may be null for inactive historical data.
9. **GL/Company boundary:** authoritative Company source, whether `GLAccountId`, `CustomerPostingGroupCode`, or both are required, and exact active/approved/postable/version rules.
10. **Document Master boundary:** Customer entity role value, mandatory-document behavior, permitted document metadata, and exact ownership of DMS/blob fields; raw document bytes must remain out of Customer persistence unless explicitly approved.
11. **India Address Control:** the authoritative address contract, validation rules, PIN/city/state semantics, whether address values or stable references are persisted, and whether the current deferred adapter is acceptable.
12. **Application workflow:** whether Customer Create/Modify/Query are the complete modes; approval, submit, reject, deactivate, merge, delete, duplicate review, and bulk import behavior are not confirmed.
13. **Customer status:** whether `CustomerLifecycleStatus.Active/Inactive` is sufficient; who can change status and whether inactive Customers remain queryable/selectable.
14. **Modify semantics:** which fields are immutable after create, whether child collections replace or patch, whether a same-value update is a no-op, concurrency/version behavior, and whether `CopyCommunicationAddressToPermanent` is part of the contract.
15. **Bank invariant:** whether a non-empty bank collection must have exactly one default, whether zero defaults are valid, and duplicate-bank-key scope. The existing `BankAccountRules` requires exactly one default and the public controller does not expose `ClearDefaultBankAccountCommand`; this needs confirmation.
16. **Customer audit:** whether create, modify, child changes, status changes, and reference changes are all audited; whether `ChangedFieldsJson` is sufficient; retention, read API, immutability, and actor claim source.
17. **Atomicity:** whether Customer, all children, audit, numbering issuance, and any external boundary side effects must commit atomically; current Customer commands use `ICustomerUnitOfWork`, while numbering issuance saves its own sequence state.
18. **Query contract:** whether Customer query must return `PaginatedResponse<CustomerResponse>` with validation for page 1–100 or retain the current `IReadOnlyCollection<CustomerResponse>` response and page-clamping behavior.
19. **Search/order:** exact search fields, case/trim semantics, ordering, active/status filtering, Company scoping, and page-size defaults. Current repository searches CustomerCode/FirstName/SecondName and orders CustomerCode; the request default is page size 25.
20. **Catalog API:** whether `/api/customer-onboarding/catalog` is required, which lookups/nationalities/GL/documents it returns, and whether it is a backend endpoint to add or a frontend call to remove. The current UI calls it, but no backend action was found.
21. **Authorization:** whether all Customer reads/writes/child operations remain `SystemAdmin`-only or whether authorized business roles may read/create/modify; exact actor claim precedence (`NameIdentifier`, `sub`, `Name`) requires confirmation.
22. **Gateway route:** whether `/api/ua/api/customer-onboarding/**` and `/api/customer-onboarding/**` are the final paths, and whether the existing `CustomerOnboardingAliasRoute` `PathPattern` transform must be retained or normalized to the other alias convention.
23. **Error payload:** whether the canonical client field is `ProblemDetails.Detail`, `message`, or both. Current middleware writes `Detail` and `code`; `src/services/api.ts` reads `data.message`; the Customer page reads `data.detail`.
24. **Frontend scope:** whether the existing multi-tab `CustomerOnboarding` page is the required screen or a separate Customer Master list/grid/search page is needed; whether add/modify/query routes and navigation already satisfy the ticket.
25. **Frontend service shape:** whether to preserve `CustomerOnboardingService` in `src/services/interfaces/customerOnboarding.service.ts` or normalize it into `customerOnboarding.interface.ts` plus barrel exports like Lookup/Nationality.
26. **Frontend paging and catalog state:** whether the UI needs server-side Customer pagination, catalog loading on mount, and explicit loading/error state for each tab; current UI query accepts a string and returns an array.
27. **Frontend mock behavior:** whether mock Customer data may use fixed English-only IDs/codes and how it should simulate duplicate/reference/boundary errors; the current mock does not match real error shapes.
28. **Document upload scope:** whether the separate `customer-document-blob-upload` feature is included in this customer-master ticket. Existing material treats blob upload as a separate feature and stores bytes outside SQL.
29. **Customer Reference/Mind Mapping scope:** whether those are deferred as current existing specs say, or part of the new task. Existing code/tests deliberately assert that the UI does not render deferred Customer Reference, Mind Mapping, or MassTransit controls.
30. **External integrations:** whether PayU, Python Cron KYC, Finnova-Document-Verifier, MassTransit/message broker, DMS, account/contract, and behavior-model training are current scope. Existing material marks most as deferred/boundary dependencies.
31. **Migration/deployment:** whether Customer tables already exist in deployed databases despite no repository migration artifact, and whether the user expects the spec tasks to generate/apply a migration.
32. **FRS language examples:** whether any source examples use non-India/Oman identity/document values. Product context requires mapping to confirmed India-compatible values and forbids Oman, Arabic, or other non-English sample/seed/test data.
33. **Localization:** whether any future multi-language requirement exists. Current product context requires one English field, no translation scaffolding, and no RTL; do not add `NameEn`/`NameAr` or equivalent.
34. **Generated/source boundaries:** whether existing `Customer` source marked `// CREATE` was intentionally applied and authoritative, and whether any prior customer workflow changed files outside the currently inspected tree.

## Recommended design-resolution sequence

Before authoring final requirements/design/tasks, resolve the assumptions in this order:

1. Obtain FINNOVA-51..55 and the Customer Master FRS, then map each approved field/workflow criterion to the existing `CustomerContracts.cs` and `Customer` aggregate.
2. Confirm whether the deliverable is the existing Customer Onboarding slice, a new Customer Master list, or both. Keep Customer Reference/Mind Mapping, blob upload, PayU/Python KYC, and MassTransit deferred unless explicitly included.
3. Decide whether query becomes a validated `PaginatedResponse<CustomerResponse>` API. If yes, update repository/handler/controller/frontend model/service/tests together; do not leave the current array/paging mismatch.
4. Decide the catalog endpoint contract and either implement it end-to-end or remove/defer the frontend `loadCatalog` call. Do not ship a permanently unreachable UI request.
5. Confirm authorization roles, actor claims, audit scope, error payload compatibility, and gateway route.
6. Confirm migration ownership and add one explicit migration/deployment task if the Customer schema is not already present.
7. Only then write code blocks into `tasks.md`, using exact current signatures and paths and sequencing backend dependencies before frontend dependents.

## Decomposition decision

This investigation does **not** create FEAT artifacts or replace the remaining workflow tail. The evidence shows one Customer Onboarding/Customer Master vertical slice with tightly coupled backend and frontend halves. The current task is document-only investigation, not two independently implementable features; splitting backend and frontend into concurrent features would obscure the contract decisions above and could race on the shared API/UI boundary. The existing design loop should consume this report and author the three spec documents plus `.config.kiro`.
