# Tasks — Resolve Build and Unit-Test Failures

This document is the implementation sequence for the repair. It is based on the current on-disk files read before authoring this document. `VERIFY` tasks do not fabricate edits: the displayed snippets are the current state that the implementer must preserve. The only mandatory source/test edit is the exact-type assertion in the ninth task. All build, test, and frontend commands below are implementer commands; this workflow has not run them.

The historical baseline is documented in `.agents/tasks/fix-api-build/report.md`, `.agents/tasks/fix-api-build/review.md`, `.agents/tasks/resolve-api-test-failures/report.md`, and `.agents/tasks/resolve-api-test-failures/review.md`. The reports are historical evidence only.

## VERIFY .agents/tasks/fix-api-build/report.md

**Task 1 — Inspect and isolate the baseline.** The implementer must read all four historical reports/reviews and inspect the current branch and working tree before applying any repair. Preserve the unrelated JV-posting modifications and untracked files. Do not reset, clean, delete, or overwrite them. These are read-only inspection commands:

```powershell
# Implementer inspection commands. Do not run build, restore, or test commands here.
Get-Content ".agents/tasks/fix-api-build/report.md"
Get-Content ".agents/tasks/fix-api-build/review.md"
Get-Content ".agents/tasks/resolve-api-test-failures/report.md"
Get-Content ".agents/tasks/resolve-api-test-failures/review.md"
git status --short --branch
git diff --stat
```

The current status includes unrelated JV-posting work under `Finnova.Models`, `Finnova.Service`, `Finnova.SystemAdminService`, and `Finnova.Tests`. Preserve that work.

## VERIFY Finnova.Tests/Finnova.Tests.csproj

**Task 2 — Repair project/package compatibility only if absent.** The current file already contains the required xUnit compatibility state and all required host/project references. Verify that the following exact current XML remains present. Do not add a duplicate package or reference when this state is present.

Current package alignment in `Finnova.Tests/Finnova.Tests.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
  <PackageReference Include="xunit" Version="2.9.2" />
  <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  <PackageReference Include="xunit.extensibility.core" Version="2.9.2" />
  <PackageReference Include="Moq" Version="4.20.72" />
  <PackageReference Include="FsCheck.Xunit" Version="2.16.6" />
  <PackageReference Include="FluentValidation" Version="11.11.0" />
  <PackageReference Include="coverlet.collector" Version="6.0.2" />
  <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.11" />
  <PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="8.19.2" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.0.0" />
</ItemGroup>
```

Current test project references:

```xml
<ItemGroup>
  <ProjectReference Include="..\Finnova.Service\Finnova.Service.csproj" />
  <ProjectReference Include="..\Finnova.Models\Finnova.Models.csproj" />
  <ProjectReference Include="..\Finnova.Repository\Finnova.Repository.csproj" />
  <ProjectReference Include="..\Finnova.SystemAdminService\Finnova.SystemAdminService.csproj" />
  <ProjectReference Include="..\Finnova.AccountsService\Finnova.AccountsService.csproj" />
</ItemGroup>
```

The current `Finnova.Service/Finnova.Service.csproj` also contains the required framework reference:

```xml
<ItemGroup>
  <FrameworkReference Include="Microsoft.AspNetCore.App" />
</ItemGroup>
```

The current solution project list in `Finnova.Backend.slnx` is:

```xml
<Solution>
  <Project Path="Finnova.AccountsService/Finnova.AccountsService.csproj" />
  <Project Path="Finnova.ApiGateway/Finnova.ApiGateway.csproj" />
  <Project Path="Finnova.Models/Finnova.Models.csproj" />
  <Project Path="Finnova.Repository/Finnova.Repository.csproj" />
  <Project Path="Finnova.Service/Finnova.Service.csproj" />
  <Project Path="Finnova.Tests/Finnova.Tests.csproj" />
  <Project Path="Finnova.SystemAdminService/Finnova.SystemAdminService.csproj" />
  <Project Path="Finnova.UAService/Finnova.UAService.csproj" />
</Solution>
```

If any of these exact compatibility repairs is absent in the implementer's checkout, apply only the missing current-state equivalent in the owning file. Do not remove tests, suppress restore warnings, or add a reverse project reference.

## VERIFY Finnova.Models/Domain/Exceptions/IdentityVerificationExceptions.cs

**Task 3 — Repair domain and contract compilation only if absent.** Verify the constructor-dependent property assignment, the legacy/active workflow interface split, the amortization switch syntax, and the provider executor signature. The current exception constructor is:

```csharp
public sealed class IdentityVerificationPersistenceException : IdentityVerificationException
{
    public IdentityVerificationPersistenceException(Exception inner) : base("The verification result could not be recorded.")
    {
        Inner = inner;
    }

    public override string ErrorCode => "ERR-IVF-500";
    public Exception Inner { get; }
}
```

The current amortization switch in `Finnova.Service/Applications/Financial/AmortizationCalculator.cs` is:

```csharp
var periods = t.RepaymentFrequency switch
{
    RepaymentFrequency.Monthly => 12,
    RepaymentFrequency.Quarterly => 4,
    RepaymentFrequency.HalfYearly => 2,
    RepaymentFrequency.Annual => 1,

    _ => throw new ArgumentOutOfRangeException()
};
```

The current workflow contracts are intentionally split. `Finnova.Service/IdentityVerification/Workflow/IVerificationWorkflowCoordinator.cs` contains:

```csharp
public interface ILegacyVerificationWorkflowCoordinator
{
    Task<VerificationWorkflowResponse> RunPanAndNameWorkflowAsync(PanAndNameWorkflowRequest request, CancellationToken ct);
    Task<VerificationWorkflowResponse> RunAadhaarAndAddressWorkflowAsync(AadhaarAndAddressWorkflowRequest request, CancellationToken ct);
    Task<VerificationWorkflowResponse> RunFullWorkflowAsync(FullVerificationWorkflowRequest request, CancellationToken ct);
}
```

`VerificationWorkflowCoordinator.cs` contains the active contract and implementation boundary:

```csharp
public interface IVerificationWorkflowCoordinator
{
    Task<VerificationWorkflowResponse> ExecuteAsync(VerificationWorkflowRequest request, CancellationToken ct);
}

public sealed class VerificationWorkflowCoordinator(
    IIdentityVerificationRepository repository,
    IProtectedDocumentStore store,
    IProviderCallExecutor executor,
    IEnumerable<IIdentifierValidationAdapter> identifiers,
    IComparisonAdapterResolver comparisons,
    IRequestContextAccessor context) : IVerificationWorkflowCoordinator
```

The current provider executor contract and implementation signature are:

```csharp
Task<T> ExecuteAsync<T>(
    Func<CancellationToken, Task<T>> execFunc,
    VerificationOperationType operationType,
    string correlationId,
    CancellationToken ct = default);
```

```csharp
public async Task<T> ExecuteAsync<T>(
    Func<CancellationToken, Task<T>> execFunc,
    VerificationOperationType operationType,
    string correlationId,
    CancellationToken ct = default)
```

Preserve the current operation/correlation/cancellation ordering and imports. If a compile repair is missing, correct the owning declaration or caller without changing route shapes, retry semantics, exception codes, or provider behavior.

## VERIFY Finnova.Repository/Interfaces/ApplicationReferencePorts.cs

**Task 4 — Repair dependency direction and application validation only if absent.** Verify that application reference ports remain in the repository-facing contracts assembly, that adapters implement those interfaces, and that service consumers depend on the repository contracts rather than the service project. The current shared port declarations are:

```csharp
namespace Finnova.Repository.Interfaces;

public interface ILineOfBusinessReferencePort
{
    Task<List<LineOfBusinessRefResponse>> GetActiveAsync(CancellationToken ct);
    Task<bool> ExistsActiveAsync(Guid id, CancellationToken ct);
}

public interface ICustomerReferencePort
{
    Task<(List<CustomerReferenceOptionResponse> Items, int Total)> SearchActiveAsync(string? search, int page, int pageSize, CancellationToken ct);
    Task<bool> ExistsActiveAsync(Guid id, CancellationToken ct);
}

public interface IBranchReferencePort
{
    Task<List<ApplicationReferenceOptionResponse>> GetActiveAsync(Guid? lineOfBusinessId, CancellationToken ct);
    Task<bool> ExistsActiveAsync(Guid id, CancellationToken ct);
}

public interface ILookupReferencePort
{
    Task<List<LookupDropdownItemResponse>> GetActiveAsync(string module, string type, CancellationToken ct);
    Task<bool> ExistsActiveAsync(string module, string type, string code, CancellationToken ct);
}
```

The service-local ports file must retain only the validator boundary:

```csharp
namespace Finnova.Service.Applications.Ports;

public interface IApplicationReferenceValidator
{
    Task ValidateAsync(Guid lobId, Guid customerId, Guid branchId, string productCode, string schemeCode, CancellationToken ct);
}
```

The current repository registration is present in `Finnova.Repository/DependencyInjection.cs`:

```csharp
services.AddScoped<ILineOfBusinessReferencePort, LineOfBusinessReferenceAdapter>();
services.AddScoped<ICustomerReferencePort, CustomerReferenceAdapter>();
services.AddScoped<IBranchReferencePort, BranchReferenceAdapter>();
services.AddScoped<ILookupReferencePort, LookupReferenceAdapter>();
```

The current service validator consumes `Finnova.Repository.Interfaces` and uses all five active-reference checks:

```csharp
public sealed class ApplicationReferenceValidator : IApplicationReferenceValidator
{
    private readonly ILineOfBusinessReferencePort _lob;
    private readonly ICustomerReferencePort _customer;
    private readonly IBranchReferencePort _branch;
    private readonly ILookupReferencePort _lookup;
    private readonly IConfiguration _configuration;

    public ApplicationReferenceValidator(ILineOfBusinessReferencePort lob, ICustomerReferencePort customer,
        IBranchReferencePort branch, ILookupReferencePort lookup, IConfiguration configuration)
        => (_lob, _customer, _branch, _lookup, _configuration) = (lob, customer, branch, lookup, configuration);

    public async Task ValidateAsync(Guid lobId, Guid customerId, Guid branchId, string productCode, string schemeCode, CancellationToken ct)
    {
        var module = _configuration["ApplicationProcessing:LookupModule"] ?? throw new ApplicationConfigurationException("ApplicationProcessing:LookupModule is required.");
        var productType = _configuration["ApplicationProcessing:ProductLookupType"] ?? throw new ApplicationConfigurationException("ApplicationProcessing:ProductLookupType is required.");
        var schemeType = _configuration["ApplicationProcessing:SchemeLookupType"] ?? throw new ApplicationConfigurationException("ApplicationProcessing:SchemeLookupType is required.");
        var checks = await Task.WhenAll(_lob.ExistsActiveAsync(lobId, ct), _customer.ExistsActiveAsync(customerId, ct),
            _branch.ExistsActiveAsync(branchId, ct), _lookup.ExistsActiveAsync(module, productType, productCode.Trim(), ct),
            _lookup.ExistsActiveAsync(module, schemeType, schemeCode.Trim(), ct));
        if (!checks[0]) throw new ApplicationReferenceInvalidException("Line of Business");
        if (!checks[1]) throw new ApplicationReferenceInvalidException("Customer");
        if (!checks[2]) throw new ApplicationReferenceInvalidException("Branch");
        if (!checks[3]) throw new ApplicationReferenceInvalidException("Product");
        if (!checks[4]) throw new ApplicationReferenceInvalidException("Scheme");
    }
}
```

Verify the adapters under `Finnova.Repository/Repositories`, the service consumers under `Finnova.Service/Applications`, `Finnova.Repository/DependencyInjection.cs`, and the current test API/signature call sites. The current FluentValidation repair is direct rules, not the removed `Transform` API:

```csharp
public sealed class UpdateApplicationCommandValidator : AbstractValidator<Applications.Commands.UpdateApplicationCommand>
{
    public UpdateApplicationCommandValidator()
    {
        RuleFor(x => x.LineOfBusinessId).NotEmpty(); RuleFor(x => x.CustomerId).NotEmpty(); RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.ProductCode).NotEmpty().MaximumLength(50); RuleFor(x => x.SchemeCode).NotEmpty().MaximumLength(50);
        RuleFor(x => x.FinanceAmount).GreaterThan(0).LessThanOrEqualTo(ApplicationValidationRules.MaxFinanceAmount).Must(ApplicationValidationRules.HasPaisaScale);
        RuleFor(x => x.IrrPercent).InclusiveBetween(0, 100); RuleFor(x => x.Tenure).InclusiveBetween(1, ApplicationValidationRules.MaxTenure);
        RuleFor(x => x.RepaymentFrequency).Must(ApplicationValidationRules.IsSupported); RuleFor(x => x.ProcessingCharges).GreaterThanOrEqualTo(0).Must(x => !x.HasValue || ApplicationValidationRules.HasPaisaScale(x.Value));
        RuleFor(x => x.Id).NotEmpty();
    }
}
```

If any current compile correction is absent, apply only that correction. Do not move the ports back into the service project or add a repository-to-service reference.

## VERIFY Finnova.SystemAdminService/Program.cs

**Task 5 — Verify host and gateway wiring.** Preserve authentication before authorization, the `SystemAdmin` role policy, both public testability shims, host-specific middleware, and the existing gateway aliases. The current SystemAdmin policy and registration are:

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SystemAdmin", p => p.RequireRole("SystemAdmin"));
    options.AddPolicy("DOC_MASTER_MAKER", p => p.RequireRole("DOC_MASTER_MAKER"));
    options.AddPolicy("DOC_MASTER_CHECKER", p => p.RequireRole("DOC_MASTER_CHECKER"));
    options.AddPolicy("JveOperationsRead", policy => policy.RequireClaim("permission", "jve.operations.read"));
    options.AddPolicy("JveOperationsRecovery", policy => policy.RequireClaim("permission", "jve.operations.recovery"));
});

builder.Services.AddScoped<IRequestContextAccessor, RequestContextAccessor>();
builder.Services.AddScoped<IProviderCallExecutor, ProviderCallExecutor>();
builder.Services.AddScoped<IVerificationWorkflowCoordinator, VerificationWorkflowCoordinator>();
```

The current Accounts host retains the application role and middleware order:

```csharp
options.AddPolicy("ApplicationProcessor", p => p.RequireRole("ApplicationProcessor"));

app.UseMiddleware<Finnova.AccountsService.Middlewares.ExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
```

The SystemAdmin middleware currently emits the identity error code and correlation identifier:

```csharp
var problem = new ProblemDetails { Status = status, Title = code, Detail = detail };
problem.Extensions["code"] = code;
problem.Extensions["correlationId"] = ctx.TraceIdentifier;
ctx.Response.StatusCode = status;
ctx.Response.Headers["X-Correlation-Id"] = ctx.TraceIdentifier;
await ctx.Response.WriteAsJsonAsync(problem);
```

The Accounts middleware currently preserves the application not-found mapping:

```csharp
ApplicationProcessingException a => (a.ErrorCode switch
{
    "ERR-APP-404" => StatusCodes.Status404NotFound,
    "ERR-APP-503" => StatusCodes.Status503ServiceUnavailable,
    _ => StatusCodes.Status409Conflict
}, a.ErrorCode, a.Message),
```

The current `Finnova.ApiGateway/appsettings.json` must retain the following route/cluster contracts. Do not add a route for this repair when these aliases are present:

```json
"ua-identity-verification-alias-route": {
  "ClusterId": "systemadmin-cluster",
  "Match": {
    "Path": "/api/ua/api/identity-verification/{**catch-all}"
  },
  "Transforms": [
    { "PathRemovePrefix": "/api/ua/api" },
    { "PathPrefix": "/api" }
  ]
},
"ua-application-processing-alias-route": {
  "ClusterId": "accounts-cluster",
  "Match": { "Path": "/api/ua/api/application-processing/{**catch-all}" },
  "Transforms": [
    { "PathRemovePrefix": "/api/ua/api" },
    { "PathPrefix": "/api" }
  ]
}
```

Also preserve `identity-verification-catch-all`, `identity-verification-root`, both root aliases, and the existing `systemadmin-cluster` and `accounts-cluster` destinations. If a required alias is actually absent in the implementer's checkout, stop and request scope confirmation rather than inventing a new route or collapsing hosts.

## VERIFY Finnova.Service/IdentityVerification/Validation/DocumentInputValidator.cs

**Task 6 — Preserve or restore production behavioral repairs.** The current document validator checks accepted MIME type, filename extension agreement, and signature together before provider or persistence work:

```csharp
public void Validate(string fileName, string contentType, long sizeBytes, ReadOnlySpan<byte> header)
{
    if (sizeBytes > _options.MaxDocumentBytes) throw new IdentityVerificationDocumentSizeException();
    if (!_options.AcceptedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase) || !IdentityVerificationRules.HasAllowedExtension(fileName, new[] { contentType }) || !HasSignature(contentType, header)) throw new IdentityVerificationDocumentFormatException();
}
```

The current controller retains multipart metadata and normalizes a missing submitted value without bypassing domain validation:

```csharp
[HttpPost("verify-pan-name"), Consumes("multipart/form-data")] public Task<ActionResult<ComparisonVerificationResult>> VerifyPanName([FromForm] ComparisonForm form) => UploadAndSend(form, id => mediator.Send(new VerifyPanNameCommand(new(form.SubmittedValue ?? string.Empty, form.ToSource(id)), Actor()), HttpContext.RequestAborted));
[HttpPost("verify-aadhar-address"), Consumes("multipart/form-data")] public Task<ActionResult<ComparisonVerificationResult>> VerifyAadharAddress([FromForm] ComparisonForm form) => UploadAndSend(form, id => mediator.Send(new VerifyAadharAddressCommand(new(form.SubmittedValue ?? string.Empty, form.ToSource(id)), Actor()), HttpContext.RequestAborted));
```

The current input model remains nullable at the HTTP boundary:

```csharp
public sealed class ComparisonForm
{
    public string? SubmittedValue { get; set; }
    public IFormFile? DocumentFile { get; set; }
    public string? ExtractedValuesJson { get; set; }
    public string? MappingProfile { get; set; }
    public Guid? SessionId { get; set; }
}
```

Verify these exact behaviors and the existing document examples. Do not remove `Consumes`, accept mismatched extension/MIME combinations, skip signature checks, weaken authorization, or change an API contract.

## VERIFY Finnova.Tests/Integration/IdentityVerificationApiTests.cs

**Task 7 — Preserve or restore integration-host repairs.** The current protected multipart fixtures use `MultipartFormDataContent`, so authorization is evaluated against the endpoint's declared media type:

```csharp
private static readonly Func<HttpRequestMessage>[] ProtectedRequests =
[
    () => new(HttpMethod.Post, "/api/identity-verification/verify-pan-name")
    {
        Content = new MultipartFormDataContent()
    },
    () => new(HttpMethod.Post, "/api/identity-verification/verify-aadhar-address")
    {
        Content = new MultipartFormDataContent()
    },
```

The current partial-workflow assertion verifies an addressable JSON property with a null value:

```csharp
Assert.True(body.TryGetProperty("aadhaarAddressResult", out var aadhaarAddressResult));
Assert.Equal(JsonValueKind.Null, aadhaarAddressResult.ValueKind);
```

The current application-processing factory removes both generic and provider-specific EF options before adding InMemory:

```csharp
services.RemoveAll<DbContextOptions<FinnovaDbContext>>();
services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<FinnovaDbContext>>();
services.AddDbContext<FinnovaDbContext>(o => o.UseInMemoryDatabase("application-processing-tests"));
```

The current application integration assertion preserves the stable not-found code:

```csharp
[Fact] public async Task AuthorizedUnknownIdReturns404AndStableCode() { var response = await Client("ApplicationProcessor").GetAsync($"/api/application-processing/{Guid.NewGuid()}"); Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); var body = await response.Content.ReadAsStringAsync(); Assert.Contains("ERR-APP-404", body); }
```

Verify the full current files, including JWT prerequisite handling and existing authorization tests. Do not convert multipart requests back to JSON, require omitted nullable properties, leave SQL Server options registered alongside InMemory, or weaken the 404 assertion.

## VERIFY Finnova.Tests/ApplicationProcessing/AmortizationCalculatorProperties.cs

**Task 8 — Preserve or restore focused property/example repairs.** All affected FsCheck properties must retain at least 100 generated cases. The current deterministic property compares scalar values and installment values by sequence equality:

```csharp
[Property(MaxTest = 100)] // Feature: application-processing, Property 8: Deterministic generation
public Property Deterministic(PositiveInt seed) { var t = Terms(seed.Get % 100000m + 1m, 7m, 10, RepaymentFrequency.Monthly, new DateOnly(2025, 1, 1)); var c = new AmortizationCalculator(); var first = c.Calculate(t); var second = c.Calculate(t); return (first.PeriodicInterestRate == second.PeriodicInterestRate && first.EmiAmount == second.EmiAmount && first.TotalInstallmentAmount == second.TotalInstallmentAmount && first.TotalInterestAmount == second.TotalInterestAmount && first.TotalPrincipalAmount == second.TotalPrincipalAmount && first.Installments.SequenceEqual(second.Installments)).ToProperty(); }
```

The current identity property uses generated adapter outcomes and checks each generated status, one call per adapter, and independent results:

```csharp
var panIndependent = pan.Calls == 1 && panResult.Status == panStatus;
var aadhaarIndependent = aadhaar.Calls == 1 && aadhaarResult.Status == aadhaarStatus;
return (panIndependent && aadhaarIndependent && panResult != aadhaarResult).ToProperty();
```

The current provider-neutral reflection expectation includes the execution-context field and rejects provider-, secret-, and token-specific names:

```csharp
nameof(ProviderNeutralComparisonInput.MappingProfile),
nameof(ProviderNeutralComparisonInput.ActorReference)
```

```csharp
var onlyNeutralFields = publicFields.SetEquals(expectedFields)
    && !publicFields.Any(name => name.Contains("Provider", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Secret", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Token", StringComparison.OrdinalIgnoreCase));
```

The current document examples retain the `.png`/`application/pdf` mismatch case:

```csharp
[InlineData("identity.png", "application/pdf")]
public void UnsupportedDocumentExtensionOrMimeTypeIsRejected(string fileName, string contentType)
{
    var exception = Assert.Throws<IdentityVerificationDocumentFormatException>(() =>
        CreateDocumentValidator().Validate(fileName, contentType, 8, "%PDF-1.7"u8.ToArray()));

    Assert.Equal("ERR-IVF-415", exception.ErrorCode);
}
```

Verify these current assertions in `Finnova.Tests/ApplicationProcessing/AmortizationCalculatorProperties.cs`, `Finnova.Tests/Unit/IdentityVerificationProperties.cs`, and `Finnova.Tests/Unit/IdentityVerificationExamples.cs`. Do not replace value comparisons with reference comparisons, hard-code all generated outcomes to success, demand a removed contract member, or remove the mismatch example.

## MODIFY Finnova.Tests/Unit/IdentityVerificationProperties.cs

**Task 9 — Restore the exact exception-type assertion last among source/test edits.** In `Finnova.Tests/Unit/IdentityVerificationProperties.cs`, replace the current method body shown below. The method is `IdentityVerificationProperties.InvalidRecordShapeIsRejected`, immediately after `IdentifierAndComparisonDomainsAreExclusive`. Replace the entire current method, from its attribute through its closing brace, with the exact method below. This is the mandatory behavioral test repair.

Current on-disk snippet to replace:

```csharp
[Property(MaxTest = 100)] public Property InvalidRecordShapeIsRejected(NonEmptyString _)
{
    var invalid = new ValidationRecord { OperationType = VerificationOperationType.PanIdentifierValidation, ComparisonOutcome = ComparisonOutcome.Match };
    var threw = false;
    try { IdentityVerificationInvariants.EnsureResultDomain(invalid); }
    catch (InvalidOperationException) { threw = true; }
    return threw.ToProperty();
}
```

Paste this replacement at the same class level, directly before the comment `// Feature: pan-aadhar-validation, Property 2: Rejected input makes no provider call.`:

```csharp
[Property(MaxTest = 100)] public Property InvalidRecordShapeIsRejected(NonEmptyString _)
{
    var invalid = new ValidationRecord { OperationType = VerificationOperationType.PanIdentifierValidation, ComparisonOutcome = ComparisonOutcome.Match };
    Exception? exception = null;
    try { IdentityVerificationInvariants.EnsureResultDomain(invalid); }
    catch (Exception ex) { exception = ex; }
    return (exception?.GetType() == typeof(InvalidOperationException)).ToProperty();
}
```

This keeps the invalid record and the property-based test. A subtype of `InvalidOperationException`, an unrelated exception, or no exception must make the property fail. Do not replace this with `catch (InvalidOperationException)` or a broad assignability assertion.

## MODIFY Finnova.Tests/Finnova.Tests.runsettings

**Task 10 — Run implementer backend verification with a process-scoped JWT key.** The current runsettings file contains a persisted JWT test key, which conflicts with the process-scoped-key requirement. Before validation, remove that persisted environment-variable entry. The persisted value is intentionally not reproduced in this document. Replace the complete current file:

```xml
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <RunConfiguration>
    <EnvironmentVariables>
      <!-- A FINNOVA_TEST_JWT_KEY entry is currently persisted here; remove this block. -->
    </EnvironmentVariables>
  </RunConfiguration>
</RunSettings>
```

with this key-free file:

```xml
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <RunConfiguration />
</RunSettings>
```

Then use the validation commands at the end of this document. Set `FINNOVA_TEST_JWT_KEY` only in the PowerShell process that launches the JWT-dependent test command. Do not put a key in source, configuration, runsettings, logs, reports, or Git history. If the implementer's current runsettings file is already key-free, treat this subtask as VERIFY ONLY.

## VALIDATION — IMPLEMENTER COMMANDS ONLY

The following commands are not results from this workflow. The eventual implementer runs them after applying the ordered tasks. The key is entered into the launching PowerShell process and removed from that process after each test command. Do not echo or commit the key.

```powershell
# Implementer validation: compile the complete backend solution.
dotnet build ".\Finnova.Backend.slnx" --no-restore --verbosity:minimal

# Implementer validation: compile the test project separately.
dotnet build ".\Finnova.Tests\Finnova.Tests.csproj" --no-restore --verbosity:minimal

# Implementer validation: run the affected groups with an ephemeral process-scoped key.
$env:FINNOVA_TEST_JWT_KEY = Read-Host "Enter an ephemeral FINNOVA_TEST_JWT_KEY for this process"
dotnet test ".\Finnova.Tests\Finnova.Tests.csproj" --no-build --no-restore --filter "FullyQualifiedName~IdentityVerification|FullyQualifiedName~ApplicationProcessing|FullyQualifiedName~AmortizationCalculator" --verbosity:minimal
Remove-Item Env:FINNOVA_TEST_JWT_KEY

# Implementer validation: run the complete Finnova.Tests suite with a new process-scoped key.
$env:FINNOVA_TEST_JWT_KEY = Read-Host "Enter an ephemeral FINNOVA_TEST_JWT_KEY for this process"
dotnet test ".\Finnova.Tests\Finnova.Tests.csproj" --no-build --no-restore --verbosity:minimal
Remove-Item Env:FINNOVA_TEST_JWT_KEY
```

Expected outcome, attributed to the historical reports rather than this workflow: the solution build and test-project build succeeded with warnings only; the focused historical run reported 92 total, 92 passed, 0 failed, 0 skipped; and the complete historical `Finnova.Tests` run reported 555 total, 555 passed, 0 failed, 0 skipped. The implementer must report the current results independently. A missing or blank key is a prerequisite failure, not a reason to bypass authentication or skip tests. Do not run restore, build, test, or npm commands as part of this documents-only workflow.

## VERIFY E:\Finnova\Finnova-UI\Finnova-UI

**Task 11 — Run the frontend boundary verification only if the UI repository is accessible.** No UI source change is required. The backend repair preserves API routes, gateway aliases, response shapes, authentication behavior, and error contracts. The implementer must not add UI feature code, routes, localization, RTL handling, or a new HTTP client.

```powershell
# Implementer-only boundary verification, run from the UI repository if accessible.
Set-Location "E:\Finnova\Finnova-UI\Finnova-UI"
# Use the repository's established frontend verification command, such as:
npm run build
```

If the UI repository is unavailable, record that the UI source remains unchanged and that this boundary verification could not be performed in the current environment. If it is available and the unchanged UI exposes a genuine break against the unchanged API contracts, stop and request confirmation before expanding scope. Do not modify UI source in this repair.
