# Customer Master / Customer Onboarding — Executable Tasks

**Backend:** `e:\Finnova\Finnova-API`  
**Frontend:** `E:\Finnova\Finnova-UI\Finnova-UI`  
**Technical source of truth:** `.agents\tasks\customer-master-spec\investigation.md`

Apply the tasks in order. This authoring workflow writes only the four files in this spec directory and does not edit application source, run builds, run tests, or run migrations. The commands in this file are for the implementer/release owner after copying the source edits.

All existing Customer source listed in the investigation is a prerequisite. Do not create a second Customer entity, table set, host, page, or catalog contract. Keep values English-only and India-only. Do not add bilingual fields, non-English data, RTL, Redux, or RxJS.

## Unchanged prerequisites

These stages are already implemented and are not copy/paste edits:

- Backend domain: `Finnova.Models/Domain/Entities/Customer.cs`, all current Customer child entities, and the existing Customer enums.
- Backend exceptions: `Finnova.Models/Domain/Exceptions/CustomerExceptions.cs` and its existing typed exceptions/error codes.
- Backend contracts: `Finnova.Models/Contracts/Customers/CustomerContracts.cs`, including `CustomerQueryRequest`, and `Finnova.Models/Contracts/Common/PaginatedResponse.cs`.
- EF configuration and DbSets: all nine Customer configuration files and the Customer DbSets in `Finnova.Repository/Context/FinnovaDbContext.cs`.
- Repository interfaces, Customer write/audit repositories, unit of work, and scoped DI registrations.
- `CustomerRules`, reference resolvers, `CustomerCodeGenerator`, aggregate/projection/child mappers, and all existing create/modify/child CQRS handlers.
- Existing Customer controller actions other than the query return type, typed middleware branch, gateway routes, Customer model/tabs/routes/navigation, and auth policy.

The numbered tasks below are the actual dependent edits. Each executable task is marked `CREATE <path>` or `MODIFY <path>` as required. Execute the EF/DbContext verification and schema preflight in Task 2 before applying the repository change in Task 1; the preflight is a dependency check and does not modify application source.

## Task 1 — Backend query repository

### MODIFY `Finnova.Repository/Repositories/CustomerReadRepository.cs`

Replace the complete existing `SearchAsync` method. The existing method is the one that clamps page values and applies CustomerCode and Search in separate `if` blocks. Leave the class, constructor, using directives, child includes, and interface unchanged.

```csharp
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
        var normalizedCode = request.CustomerCode.Trim().ToUpper();
        query = query.Where(x => x.CustomerCode.ToUpper() == normalizedCode);
    }
    else if (!string.IsNullOrWhiteSpace(request.Search))
    {
        var normalizedTerm = request.Search.Trim().ToUpper();
        query = query.Where(x =>
            x.CustomerCode.ToUpper().Contains(normalizedTerm) ||
            (x.FirstName != null && x.FirstName.ToUpper().Contains(normalizedTerm)) ||
            (x.SecondName != null && x.SecondName.ToUpper().Contains(normalizedTerm)));
    }

    var total = await query.CountAsync(ct);
    var items = await query
        .OrderBy(x => x.CustomerCode)
        .Skip((request.Page - 1) * request.PageSize)
        .Take(request.PageSize)
        .ToListAsync(ct);

    return (items, total);
}
```

The validator in Task 3 guarantees the page bounds before this method runs. `ToUpper()` is intentional: it is translated by EF Core and behaves consistently in SQL Server and EF InMemory tests.

## Task 2 — Backend EF/DbContext verification and safe schema deployment

### MODIFY `Finnova.Repository/Context/FinnovaDbContext.cs`

Do not insert a duplicate DbSet. Confirm that the existing class contains these exact Customer DbSets. If one is absent, add only the missing line beside the other Customer DbSets; no other DbContext change is permitted.

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

The existing `OnModelCreating` must retain:

```csharp
modelBuilder.ApplyConfigurationsFromAssembly(typeof(FinnovaDbContext).Assembly);
```

### Release-owned schema preflight, not a source edit

No Customer migration artifact was verified. The supported state for this ticket is a database whose Customer tables and configured indexes already exist. Before any deployment decision, run this read-only SQL against the target database. Do not run `dotnet ef migrations add` based only on a partial live-schema result. The predicate comparison normalizes case, brackets, whitespace, and parentheses so SQL Server metadata formatting does not create a false mismatch.

```sql
DECLARE @ExpectedTables TABLE (TableName sysname NOT NULL PRIMARY KEY);
INSERT INTO @ExpectedTables(TableName) VALUES
('Customers'), ('CustomerAddresses'), ('IndividualCustomerDetails'),
('NonIndividualCustomerDetails'), ('CustomerBankAccounts'),
('CustomerDocumentReferences'), ('CustomerTrackEntries'),
('CustomerFactoringSubLimits'), ('CustomerAuditEntries');

SELECT e.TableName AS MissingTable
FROM @ExpectedTables AS e
LEFT JOIN sys.tables AS t
  ON t.name = e.TableName AND SCHEMA_NAME(t.schema_id) = N'dbo'
WHERE t.object_id IS NULL;

DECLARE @ExpectedIndexes TABLE
(
    TableName sysname NOT NULL,
    IsUnique bit NOT NULL,
    FilterDefinition nvarchar(4000) NULL,
    ColumnList nvarchar(2000) NOT NULL
);
INSERT INTO @ExpectedIndexes(TableName, IsUnique, FilterDefinition, ColumnList) VALUES
('Customers', 1, NULL, 'CustomerCode'),
('Customers', 0, NULL, 'CompanyId,NationalityCode'),
('CustomerAddresses', 1, NULL, 'CustomerId,AddressType'),
('CustomerAddresses', 0, NULL, 'CustomerId'),
('IndividualCustomerDetails', 1, NULL, 'CustomerId'),
('NonIndividualCustomerDetails', 1, NULL, 'CustomerId'),
('CustomerBankAccounts', 1, NULL, 'CustomerId,NormalizedDuplicateKey'),
('CustomerBankAccounts', 0, NULL, 'CustomerId'),
('CustomerBankAccounts', 1, '[IsDefault]=1', 'CustomerId'),
('CustomerDocumentReferences', 1, NULL, 'CustomerId,DocumentDefinitionId'),
('CustomerDocumentReferences', 0, NULL, 'CustomerId'),
('CustomerTrackEntries', 1, NULL, 'CustomerId,AccountNumber,TrackTypeCode'),
('CustomerTrackEntries', 0, NULL, 'CustomerId'),
('CustomerFactoringSubLimits', 0, NULL, 'CustomerId'),
('CustomerAuditEntries', 0, NULL, 'CustomerId,CreatedAtUtc'),
('CustomerAuditEntries', 0, NULL, 'ActorId');

;WITH ActualIndexColumns AS
(
    SELECT
        t.name AS TableName,
        i.index_id,
        CONVERT(bit, i.is_unique) AS IsUnique,
        NULLIF(LTRIM(RTRIM(i.filter_definition)), N'') AS FilterDefinition,
        STRING_AGG(CONVERT(nvarchar(max), c.name), N',')
            WITHIN GROUP (ORDER BY ic.key_ordinal) AS ColumnList
    FROM sys.tables AS t
    INNER JOIN sys.indexes AS i ON i.object_id = t.object_id
    INNER JOIN sys.index_columns AS ic
      ON ic.object_id = i.object_id AND ic.index_id = i.index_id
    INNER JOIN sys.columns AS c
      ON c.object_id = ic.object_id AND c.column_id = ic.column_id
    WHERE SCHEMA_NAME(t.schema_id) = N'dbo'
      AND i.is_hypothetical = 0
      AND i.type > 0
      AND ic.is_included_column = 0
    GROUP BY t.name, i.index_id, i.is_unique, i.filter_definition
), NormalizedActual AS
(
    SELECT TableName, IsUnique, ColumnList,
        REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UPPER(ISNULL(FilterDefinition, N'')), N' ', N''), N'[', N''), N']', N''), N'(', N''), N')', N'') AS NormalizedFilter
    FROM ActualIndexColumns
), NormalizedExpected AS
(
    SELECT TableName, IsUnique, ColumnList,
        REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UPPER(ISNULL(FilterDefinition, N'')), N' ', N''), N'[', N''), N']', N''), N'(', N''), N')', N'') AS NormalizedFilter
    FROM @ExpectedIndexes
)
SELECT e.TableName, e.IsUnique, e.FilterDefinition, e.ColumnList AS ExpectedColumnList
FROM NormalizedExpected AS e
WHERE NOT EXISTS
(
    SELECT 1
    FROM NormalizedActual AS a
    WHERE a.TableName = e.TableName
      AND a.IsUnique = e.IsUnique
      AND a.ColumnList = e.ColumnList
      AND a.NormalizedFilter = e.NormalizedFilter
)
ORDER BY e.TableName, e.ColumnList;
```

Both result sets must be empty. If they are not empty, stop and give the output to the release/schema owner for a reviewed baseline migration for a new database or a reviewed adoption/upgrade migration for the actual database. Do not generate a migration by comparing EF to a partially provisioned live database. If the release owner has a reviewed migration snapshot, the approved command is:

```powershell
dotnet ef migrations script --idempotent --project .\Finnova.Repository --startup-project .\Finnova.SystemAdminService --output .\customer-schema-reviewed.sql
```

The reviewed script, not an authoring-time conditional `migrations add`, is the deployment artifact. This workflow does not run it.

## Task 3 — Backend query CQRS slice

### CREATE `Finnova.Service/Customers/Queries/QueryCustomerValidator.cs`

```csharp
using FluentValidation;

namespace Finnova.Service.Customers.Queries;

public sealed class QueryCustomerValidator : AbstractValidator<QueryCustomer>
{
    public const int MaximumPage = 20_000_000;

    public QueryCustomerValidator()
    {
        RuleFor(x => x.Request.Page)
            .GreaterThanOrEqualTo(1)
            .LessThanOrEqualTo(MaximumPage)
            .WithMessage($"Page must be between 1 and {MaximumPage}.");

        RuleFor(x => x.Request.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("PageSize must be between 1 and 100.");
    }
}
```

### MODIFY `Finnova.Service/Customers/Queries/QueryCustomer.cs`

Replace the complete file with:

```csharp
using MediatR;
using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.Customers;
using Finnova.Repository.Interfaces;

namespace Finnova.Service.Customers.Queries;

public sealed record QueryCustomer(CustomerQueryRequest Request)
    : IRequest<PaginatedResponse<CustomerResponse>>;

public sealed class QueryCustomerHandler(
    ICustomerReadRepository read,
    ICustomerProjectionMapper mapper)
    : IRequestHandler<QueryCustomer, PaginatedResponse<CustomerResponse>>
{
    public async Task<PaginatedResponse<CustomerResponse>> Handle(
        QueryCustomer q,
        CancellationToken ct)
    {
        var result = await read.SearchAsync(q.Request, ct);
        var projected = new List<CustomerResponse>(result.Items.Count);

        foreach (var customer in result.Items)
            projected.Add(await mapper.ProjectAsync(customer, ct));

        var totalPages = result.Total == 0
            ? 0
            : (int)Math.Ceiling(result.Total / (double)q.Request.PageSize);

        return new PaginatedResponse<CustomerResponse>(
            projected,
            result.Total,
            q.Request.Page,
            q.Request.PageSize,
            totalPages);
    }
}
```

The existing assembly scanning discovers the validator. Do not add a second handler or a second validation pipeline.

## Task 4 — Backend controller, middleware, and gateway alias

### MODIFY `Finnova.SystemAdminService/Controllers/CustomerOnboardingController.cs`

Add this using beside the existing model-contract using directives if it is absent:

```csharp
using Finnova.Models.Contracts.Common;
```

Replace the existing query action that returns `Task<IReadOnlyCollection<CustomerResponse>>` with this action. Keep the controller route, authorization attribute, sender field, and every other action unchanged:

```csharp
[HttpGet]
public Task<PaginatedResponse<CustomerResponse>> Query(
    [FromQuery] CustomerQueryRequest request,
    CancellationToken ct) => sender.Send(new QueryCustomer(request), ct);
```

### MODIFY `Finnova.SystemAdminService/Middleware/ExceptionHandlingMiddleware.cs`

At the final ProblemDetails construction, replace:

```csharp
var problem = new ProblemDetails { Status = status, Title = code, Detail = detail };
problem.Extensions["code"] = code;
ctx.Response.StatusCode = status;
```

with:

```csharp
var problem = new ProblemDetails { Status = status, Title = code, Detail = detail };
problem.Extensions["code"] = code;
problem.Extensions["message"] = detail;
ctx.Response.StatusCode = status;
```

Do not remove or reorder existing typed Customer exception mappings or other feature branches. `message` is a compatibility extension; `detail` remains the RFC-compatible field.

### MODIFY `Finnova.ApiGateway/appsettings.json`

No new route or cluster is required. Confirm that the existing configuration contains both entries below. If a prior edit removed either entry, restore the exact existing route object at the same route section; do not add a second alias with a different path.

```json
"CustomerOnboardingAliasRoute": {
  "ClusterId": "systemadmin-cluster",
  "Match": { "Path": "/api/ua/api/customer-onboarding/{**catch-all}" },
  "Transforms": [
    { "PathPattern": "/api/customer-onboarding/{**catch-all}" }
  ]
}
```

Also retain the existing direct `/api/systemadmin/{**catch-all}` route to `systemadmin-cluster`. The UI must continue to compose `/api/ua/api/customer-onboarding/...` through the shared Axios base URL.

## Task 5 — Backend tests

### CREATE `Finnova.Tests/Unit/QueryCustomerValidationTests.cs`

Create this complete file:

```csharp
using FluentValidation;
using MediatR;
using Moq;
using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.Customers;
using Finnova.Repository.Interfaces;
using Finnova.Service.Behaviors;
using Finnova.Service.Customers;
using Finnova.Service.Customers.Queries;
using Xunit;

namespace Finnova.Tests.Unit;

public sealed class QueryCustomerValidationTests
{
    [Fact]
    public async Task Invalid_page_is_rejected_before_read_repository_is_called()
    {
        var read = new Mock<ICustomerReadRepository>(MockBehavior.Strict);
        var mapper = new Mock<ICustomerProjectionMapper>(MockBehavior.Strict);
        var handler = new QueryCustomerHandler(read.Object, mapper.Object);
        var behavior = new ValidationBehavior<QueryCustomer, PaginatedResponse<CustomerResponse>>(
            new[] { new QueryCustomerValidator() });
        var request = new QueryCustomer(
            new CustomerQueryRequest(null, null, null, Page: 0, PageSize: 25));

        await Assert.ThrowsAsync<ValidationException>(() => behavior.Handle(
            request,
            () => handler.Handle(request, CancellationToken.None),
            CancellationToken.None));

        read.Verify(
            x => x.SearchAsync(It.IsAny<CustomerQueryRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(20_000_001)]
    public void Invalid_page_values_are_rejected(int page)
    {
        var result = new QueryCustomerValidator().Validate(new QueryCustomer(
            new CustomerQueryRequest(null, null, null, page, 25)));
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Invalid_page_size_values_are_rejected(int pageSize)
    {
        var result = new QueryCustomerValidator().Validate(new QueryCustomer(
            new CustomerQueryRequest(null, null, null, 1, pageSize)));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Boundary_values_are_accepted()
    {
        var validator = new QueryCustomerValidator();
        Assert.True(validator.Validate(new QueryCustomer(
            new CustomerQueryRequest(null, null, null, 1, 1))).IsValid);
        Assert.True(validator.Validate(new QueryCustomer(
            new CustomerQueryRequest(null, null, null,
                QueryCustomerValidator.MaximumPage, 100))).IsValid);
    }
}
```

### MODIFY `Finnova.Tests/Integration/CustomerOnboardingApiTests.cs`

Make the following exact edits in the existing Customer integration test file.

1. Add:

```csharp
using Finnova.Models.Contracts.Common;
```

2. Wherever the query response is read as `CustomerResponse[]`, replace that read with:

```csharp
var queried = await queryResponse.Content
    .ReadFromJsonAsync<PaginatedResponse<CustomerResponse>>(JsonOptions);
Assert.NotNull(queried);
var projection = Assert.Single(queried!.Data);
```

In the generated-code flow also assert:

```csharp
Assert.Equal(1, queried.Total);
Assert.Equal(1, queried.Page);
Assert.Equal(25, queried.PageSize);
Assert.Equal(1, queried.TotalPages);
```

3. Extend the existing `ProblemDetailsDto` record from `(Status, Title, Detail, Code)` to:

```csharp
private sealed record ProblemDetailsDto(
    int? Status,
    string? Title,
    string? Detail,
    string? Code,
    string? Message);
```

Immediately after the existing status/code assertions in the Customer problem helper, add:

```csharp
Assert.Equal(code, problem!.Title);
Assert.Equal(code, problem.Code);
Assert.False(string.IsNullOrWhiteSpace(problem.Detail));
Assert.Equal(problem.Detail, problem.Message);
```

Retain only one copy of duplicate assertions if the helper already has them.

4. Add these tests before the existing `AdminClient()` helper. They use the existing factory, `AdminClient`, `DevJwt`, `JsonOptions`, `SeedMasters`, and request helpers documented by the investigation:

```csharp
[Theory]
[InlineData("page=0&pageSize=25")]
[InlineData("page=-1&pageSize=25")]
[InlineData("page=1&pageSize=0")]
[InlineData("page=1&pageSize=101")]
public async Task Query_rejects_invalid_paging_with_complete_problem_shape(string query)
{
    using var client = AdminClient();
    var response = await client.GetAsync($"/api/customer-onboarding?{query}");

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>(JsonOptions);
    Assert.NotNull(problem);
    Assert.Equal("ERR-CUSTOMER-400", problem!.Code);
    Assert.Equal("ERR-CUSTOMER-400", problem.Title);
    Assert.False(string.IsNullOrWhiteSpace(problem.Detail));
    Assert.Equal(problem.Detail, problem.Message);
}

[Fact]
public async Task Query_rejects_an_authenticated_non_admin()
{
    using var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", DevJwt.NoRole());

    var response = await client.GetAsync(
        "/api/customer-onboarding?page=1&pageSize=25");

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
}

[Theory]
[InlineData("expired")]
[InlineData("tampered")]
public async Task Query_rejects_invalid_tokens(string kind)
{
    using var client = _factory.CreateClient();
    var token = kind == "expired" ? DevJwt.Expired() : DevJwt.Tampered();
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", token);

    var response = await client.GetAsync(
        "/api/customer-onboarding?page=1&pageSize=25");

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
}
```

Add the existing test host's end-to-end query coverage for CompanyId isolation, exact trimmed CustomerCode precedence over Search, mixed-case partial CustomerCode/FirstName/SecondName matching, CustomerCode ordering, and beyond-last metadata. Use the actual `CustomerOnboardingAppFactory` seed helpers rather than introducing a second host or a static database.

## Task 6 — Frontend model, service interface, real service, mock service, selector, and barrels

### MODIFY `E:\Finnova\Finnova-UI\Finnova-UI\src\models\customerOnboarding.ts`

Make these two exact changes.

1. Replace the current `CustomerProjection` declaration with the same declaration plus these wire-name fields. Use lower camel case because System.Text.Json serializes the backend `GroupName`, `IndustryName`, `GLName`, `ConstitutionName`, and `NationalityName` properties as these JSON names. Remove the old `postingGroupName` property rather than maintaining two names:

```typescript
export interface CustomerProjection extends CustomerForm {
  id: string;
  status: string;
  group?: { code: string; name?: string };
  industry?: { code: string; name?: string };
  constitution?: { code: string; name?: string };
  nationality?: { code: string; name?: string };
  groupName?: string;
  industryName?: string;
  glName?: string;
  constitutionName?: string;
  nationalityName?: string;
  credit?: CreditDetails;
  createdAtUtc: string;
  updatedAtUtc: string;
}
```

2. Append:

```typescript
export interface CustomerQueryParams {
  search?: string;
  customerCode?: string;
  companyId?: string;
  page?: number;
  pageSize?: number;
}
```

Leave the existing form, child, and section types unchanged. `CustomerCatalog` may remain as an unused type for source compatibility, but no service method may call a catalog endpoint.

### MODIFY `E:\Finnova\Finnova-UI\Finnova-UI\src\services\interfaces\customerOnboarding.service.ts`

Replace the complete file with:

```typescript
import type {
  CustomerForm,
  CustomerOnboardingMode,
  CustomerProjection,
  CustomerQueryParams,
} from '../../models/customerOnboarding';
import type { PaginatedResponse } from '../../models/api.model';

export interface CustomerOnboardingService {
  create(request: CustomerForm): Promise<CustomerProjection>;
  modify(customerCode: string, request: CustomerForm): Promise<CustomerProjection>;
  query(params?: CustomerQueryParams): Promise<PaginatedResponse<CustomerProjection>>;
  copyCommunicationAddress(customerCode: string): Promise<CustomerProjection>;
}

export const customerOnboardingModeAllowsWrite = (mode: CustomerOnboardingMode) =>
  mode !== 'query';
```

### MODIFY `E:\Finnova\Finnova-UI\Finnova-UI\src\services\real\customerOnboarding.service.ts`

Replace the complete file with:

```typescript
import api from '../api';
import type {
  CustomerForm,
  CustomerProjection,
  CustomerQueryParams,
} from '../../models/customerOnboarding';
import type { PaginatedResponse } from '../../models/api.model';
import type { CustomerOnboardingService } from '../interfaces/customerOnboarding.service';

type QueryPayload = PaginatedResponse<CustomerProjection> | CustomerProjection[];

function normalizeQueryResponse(
  payload: QueryPayload,
  params: CustomerQueryParams,
): PaginatedResponse<CustomerProjection> {
  if (Array.isArray(payload)) {
    const page = params.page ?? 1;
    const pageSize = params.pageSize ?? 25;
    return {
      data: payload,
      total: payload.length,
      page,
      pageSize,
      totalPages: payload.length === 0 ? 0 : Math.ceil(payload.length / pageSize),
    };
  }

  return {
    data: payload.data ?? [],
    total: payload.total ?? payload.data?.length ?? 0,
    page: payload.page ?? params.page ?? 1,
    pageSize: payload.pageSize ?? params.pageSize ?? 25,
    totalPages: payload.totalPages ?? 0,
  };
}

export const realCustomerOnboardingService: CustomerOnboardingService = {
  async create(request: CustomerForm): Promise<CustomerProjection> {
    const { data } = await api.post<CustomerProjection>('/customer-onboarding', request);
    return data;
  },

  async modify(customerCode: string, request: CustomerForm): Promise<CustomerProjection> {
    const { data } = await api.put<CustomerProjection>(
      `/customer-onboarding/${encodeURIComponent(customerCode)}`,
      { ...request, customerCode },
    );
    return data;
  },

  async query(params: CustomerQueryParams = {}): Promise<PaginatedResponse<CustomerProjection>> {
    const { data } = await api.get<QueryPayload>('/customer-onboarding', {
      params: {
        search: params.search,
        customerCode: params.customerCode,
        companyId: params.companyId,
        page: params.page,
        pageSize: params.pageSize,
      },
    });
    return normalizeQueryResponse(data, params);
  },

  async copyCommunicationAddress(customerCode: string): Promise<CustomerProjection> {
    const { data } = await api.post<CustomerProjection>(
      `/customer-onboarding/${encodeURIComponent(customerCode)}/addresses/copy-communication`,
    );
    return data;
  },
};
```

### MODIFY `E:\Finnova\Finnova-UI\Finnova-UI\src\services\mock\customerOnboarding.service.ts`

Replace the complete file with:

```typescript
import type {
  CustomerForm,
  CustomerProjection,
  CustomerQueryParams,
} from '../../models/customerOnboarding';
import type { PaginatedResponse } from '../../models/api.model';
import type { CustomerOnboardingService } from '../interfaces/customerOnboarding.service';

const MAX_PAGE = 20_000_000;
const store: CustomerProjection[] = [];
let sequence = 0;

function backendError(status: number, code: string, message: string): Error {
  const error = new Error(message) as Error & {
    response: {
      status: number;
      data: {
        status: number;
        title: string;
        detail: string;
        code: string;
        message: string;
      };
    };
  };
  error.response = {
    status,
    data: { status, title: code, detail: message, code, message },
  };
  return error;
}

function validatePaging(params: CustomerQueryParams): { page: number; pageSize: number } {
  const page = params.page ?? 1;
  const pageSize = params.pageSize ?? 25;
  if (page < 1 || page > MAX_PAGE) {
    throw backendError(400, 'ERR-CUSTOMER-400', `Page must be between 1 and ${MAX_PAGE}.`);
  }
  if (pageSize < 1 || pageSize > 100) {
    throw backendError(400, 'ERR-CUSTOMER-400', 'PageSize must be between 1 and 100.');
  }
  return { page, pageSize };
}

function toPage(
  items: CustomerProjection[],
  page: number,
  pageSize: number,
): PaginatedResponse<CustomerProjection> {
  const total = items.length;
  return {
    data: items.slice((page - 1) * pageSize, page * pageSize),
    total,
    page,
    pageSize,
    totalPages: total === 0 ? 0 : Math.ceil(total / pageSize),
  };
}

export function resetMockCustomerOnboardingStore(): void {
  store.splice(0, store.length);
  sequence = 0;
}

export const mockCustomerOnboardingService: CustomerOnboardingService = {
  async create(request: CustomerForm): Promise<CustomerProjection> {
    const next = ++sequence;
    const now = new Date().toISOString();
    const result = {
      ...request,
      id: `mock-customer-${next}`,
      customerCode: `MOCK${String(next).padStart(8, '0')}`,
      status: 'Active',
      createdAtUtc: now,
      updatedAtUtc: now,
      groupName: undefined,
      industryName: undefined,
      glName: undefined,
      constitutionName: undefined,
      nationalityName: undefined,
    } as CustomerProjection;
    store.push(result);
    return result;
  },

  async modify(customerCode: string, request: CustomerForm): Promise<CustomerProjection> {
    const current = store.find((x) => x.customerCode === customerCode);
    if (!current) throw backendError(404, 'ERR-CUSTOMER-404', 'Customer was not found.');
    Object.assign(current, {
      ...request,
      customerCode,
      updatedAtUtc: new Date().toISOString(),
    });
    return current;
  },

  async query(params: CustomerQueryParams = {}): Promise<PaginatedResponse<CustomerProjection>> {
    const { page, pageSize } = validatePaging(params);
    const term = (params.search ?? '').trim().toLowerCase();
    const exactCode = (params.customerCode ?? '').trim().toLowerCase();
    const companyId = (params.companyId ?? '').trim();

    const filtered = store
      .filter((x) => !companyId || x.companyId === companyId)
      .filter((x) => {
        if (exactCode) return (x.customerCode ?? '').toLowerCase() === exactCode;
        if (!term) return true;
        return [x.customerCode, x.firstName, x.secondName]
          .some((value) => value?.toLowerCase().includes(term));
      })
      .sort((a, b) => (a.customerCode ?? '').localeCompare(b.customerCode ?? '', 'en'));

    return toPage(filtered, page, pageSize);
  },

  async copyCommunicationAddress(customerCode: string): Promise<CustomerProjection> {
    const current = store.find((x) => x.customerCode === customerCode);
    if (!current) throw backendError(404, 'ERR-CUSTOMER-404', 'Customer was not found.');
    const communication = current.addresses.find((x) => x.addressType === 'Communication');
    if (communication) {
      current.addresses = [
        ...current.addresses.filter((x) => x.addressType !== 'Permanent'),
        { ...communication, addressType: 'Permanent' },
      ];
    }
    return current;
  },
};
```

### MODIFY `E:\Finnova\Finnova-UI\Finnova-UI\src\services\customerOnboarding.service.ts`

Replace the complete selector with:

```typescript
import { realCustomerOnboardingService } from './real/customerOnboarding.service';
import { mockCustomerOnboardingService } from './mock/customerOnboarding.service';

export const customerOnboardingService =
  import.meta.env.VITE_USE_MOCK_API === 'true'
    ? mockCustomerOnboardingService
    : realCustomerOnboardingService;
```

### MODIFY frontend barrels

Append the following exact exports, without removing unrelated exports.

`E:\Finnova\Finnova-UI\Finnova-UI\src\models\index.ts`:

```typescript
export * from './customerOnboarding';
```

`E:\Finnova\Finnova-UI\Finnova-UI\src\services\interfaces\index.ts`:

```typescript
export type { CustomerOnboardingService } from './customerOnboarding.service';
export { customerOnboardingModeAllowsWrite } from './customerOnboarding.service';
```

`E:\Finnova\Finnova-UI\Finnova-UI\src\services\index.ts`:

```typescript
export { customerOnboardingService } from './customerOnboarding.service';
```

## Task 7 — Frontend page query/error behavior

### MODIFY `E:\Finnova\Finnova-UI\Finnova-UI\src\pages\CustomerOnboarding\CustomerOnboarding.tsx`

Replace the existing one-line `useEffect` that calls `query(customerCode)` with:

```typescript
useEffect(() => {
  if (!customerCode) return;
  setBusy(true);
  customerOnboardingService
    .query({ customerCode, page: 1, pageSize: 25 })
    .then((result) => result.data[0] && setModel(result.data[0]))
    .catch((e: any) => setError(
      e?.response?.data?.message ?? e?.response?.data?.detail ?? e?.message,
    ))
    .finally(() => setBusy(false));
}, [customerCode]);
```

Replace the current save catch expression with:

```typescript
} catch (e: any) {
  setError(
    e?.response?.data?.message ??
    e?.response?.data?.detail ??
    e?.message ??
    'Unable to save Customer.',
  );
} finally {
  setBusy(false);
}
```

Leave Query-mode save gating, the read-only Customer Code behavior, all eight tabs, `App.tsx` routes, and `Layout.tsx` navigation unchanged. The page must keep the current form when query or save fails.

## Task 8 — Frontend Vitest tests

### MODIFY `E:\Finnova\Finnova-UI\Finnova-UI\src\pages\CustomerOnboarding\CustomerOnboarding.test.tsx`

Make these exact edits in the existing test.

1. Remove `loadCatalog: vi.fn(),` from the service mock.
2. Add this helper immediately before the main `describe`:

```typescript
function page(rows: CustomerProjection[] = []) {
  return {
    data: rows,
    total: rows.length,
    page: 1,
    pageSize: 25,
    totalPages: rows.length === 0 ? 0 : 1,
  };
}
```

3. Replace default `query.mockResolvedValue([])` with `query.mockResolvedValue(page())`; wrap every existing projection array as `page([projection])`.
4. Replace the old query assertion with:

```typescript
await waitFor(() => expect(customerOnboardingService.query).toHaveBeenCalledWith({
  customerCode: 'CUSTOMER0001',
  page: 1,
  pageSize: 25,
}));
```

5. Change rejected response fixtures to include `data.message`, and retain assertions proving the current form remains visible after failure.

### CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\services\real\customerOnboarding.service.test.ts`

```typescript
import { beforeEach, describe, expect, it, vi } from 'vitest';
import api from '../api';
import { realCustomerOnboardingService } from './customerOnboarding.service';

vi.mock('../api', () => ({
  default: { get: vi.fn(), post: vi.fn(), put: vi.fn() },
}));

const mockedApi = api as unknown as {
  get: ReturnType<typeof vi.fn>;
  post: ReturnType<typeof vi.fn>;
  put: ReturnType<typeof vi.fn>;
};

const projection = {
  id: 'customer-id',
  customerCode: 'CUSTOMER0001',
  companyId: 'company-id',
  isCustomer: true,
  isGuarantor: false,
  isCoApplicant: false,
  customerTypeCode: 'INDIVIDUAL',
  addresses: [], bankAccounts: [], documents: [], tracks: [], factoringSubLimits: [],
  status: 'Active',
  groupName: 'Retail', industryName: 'Finance', glName: 'Customer GL',
  constitutionName: 'Private Limited', nationalityName: 'Indian',
  createdAtUtc: '2024-01-01T00:00:00Z',
  updatedAtUtc: '2024-01-01T00:00:00Z',
};

describe('realCustomerOnboardingService', () => {
  beforeEach(() => vi.clearAllMocks());

  it('queries the exact endpoint and page parameters', async () => {
    const payload = { data: [projection], total: 1, page: 1, pageSize: 25, totalPages: 1 };
    mockedApi.get.mockResolvedValue({ data: payload });

    const result = await realCustomerOnboardingService.query({
      search: 'Anita', customerCode: 'CUSTOMER0001', companyId: 'company-id', page: 1, pageSize: 25,
    });

    expect(mockedApi.get).toHaveBeenCalledWith('/customer-onboarding', {
      params: { search: 'Anita', customerCode: 'CUSTOMER0001', companyId: 'company-id', page: 1, pageSize: 25 },
    });
    expect(result).toEqual(payload);
    expect(result.data[0].groupName).toBe('Retail');
    expect(result.data[0].glName).toBe('Customer GL');
  });

  it('normalizes a legacy array response independently', async () => {
    mockedApi.get.mockResolvedValue({ data: [projection] });
    await expect(realCustomerOnboardingService.query({ page: 2, pageSize: 10 }))
      .resolves.toEqual({ data: [projection], total: 1, page: 2, pageSize: 10, totalPages: 1 });
  });

  it('posts create and puts modify through the shared client', async () => {
    mockedApi.post.mockResolvedValue({ data: projection });
    mockedApi.put.mockResolvedValue({ data: projection });
    const form = {
      companyId: 'company-id', isCustomer: true, isGuarantor: false, isCoApplicant: false,
      customerTypeCode: 'INDIVIDUAL', addresses: [], bankAccounts: [], documents: [], tracks: [], factoringSubLimits: [],
    };
    await realCustomerOnboardingService.create(form);
    await realCustomerOnboardingService.modify('CUSTOMER/0001', form);
    expect(mockedApi.post).toHaveBeenCalledWith('/customer-onboarding', form);
    expect(mockedApi.put).toHaveBeenCalledWith('/customer-onboarding/CUSTOMER%2F0001', {
      ...form, customerCode: 'CUSTOMER/0001',
    });
  });
});
```

### CREATE `E:\Finnova\Finnova-UI\Finnova-UI\src\services\mock\customerOnboarding.service.test.ts`

```typescript
import { beforeEach, describe, expect, it } from 'vitest';
import type { CustomerForm } from '../../models/customerOnboarding';
import {
  mockCustomerOnboardingService,
  resetMockCustomerOnboardingStore,
} from './customerOnboarding.service';

const form = (companyId: string, firstName: string, secondName: string): CustomerForm => ({
  companyId, isCustomer: true, isGuarantor: false, isCoApplicant: false,
  customerTypeCode: 'INDIVIDUAL', firstName, secondName,
  addresses: [], bankAccounts: [], documents: [], tracks: [], factoringSubLimits: [],
});

describe('mockCustomerOnboardingService', () => {
  beforeEach(() => resetMockCustomerOnboardingStore());

  it('filters by CompanyId and mixed-case name search', async () => {
    await mockCustomerOnboardingService.create(form('company-a', 'Anita', 'Sharma'));
    await mockCustomerOnboardingService.create(form('company-b', 'Anita', 'Sharma'));
    const result = await mockCustomerOnboardingService.query({ companyId: 'company-a', search: 'shArMa', page: 1, pageSize: 25 });
    expect(result.total).toBe(1);
    expect(result.data[0].companyId).toBe('company-a');
  });

  it('applies exact CustomerCode precedence over Search', async () => {
    const first = await mockCustomerOnboardingService.create(form('company-a', 'Alpha', 'One'));
    await mockCustomerOnboardingService.create(form('company-a', 'Beta', 'Two'));
    const result = await mockCustomerOnboardingService.query({
      customerCode: `  ${first.customerCode!.toLowerCase()}  `, search: 'Beta',
    });
    expect(result.data.map((x) => x.customerCode)).toEqual([first.customerCode]);
  });

  it('returns the complete backend error shape for invalid paging', async () => {
    await expect(mockCustomerOnboardingService.query({ page: 0 })).rejects.toMatchObject({
      response: { status: 400, data: {
        status: 400, title: 'ERR-CUSTOMER-400', detail: 'Page must be between 1 and 20000000.',
        code: 'ERR-CUSTOMER-400', message: 'Page must be between 1 and 20000000.',
      } },
    });
  });

  it('returns a backend-shaped not-found error', async () => {
    await expect(mockCustomerOnboardingService.modify('UNKNOWN', form('company-a', 'Anita', 'Sharma')))
      .rejects.toMatchObject({
        response: { status: 404, data: {
          status: 404, title: 'ERR-CUSTOMER-404', detail: 'Customer was not found.',
          code: 'ERR-CUSTOMER-404', message: 'Customer was not found.',
        } },
      });
  });
});
```

The two test import paths are intentional: from `src/services/real`, the shared Axios module is `../api`; from `src/services/mock`, the model is `../../models/customerOnboarding`.

## Verification after applying the tasks

The implementer/release owner runs these only after applying and reviewing the source edits. This authoring workflow does not run them:

```powershell
# Backend, from e:\Finnova\Finnova-API
dotnet build .\Finnova.Backend.slnx
dotnet test .\Finnova.Tests\Finnova.Tests.csproj

# Frontend, from E:\Finnova\Finnova-UI\Finnova-UI
npm run build
npm test
```

Run the read-only schema preflight in Task 2 under the approved database process. If it reports a mismatch, do not run a conditional `migrations add`; obtain the reviewed migration artifact from the schema owner.
