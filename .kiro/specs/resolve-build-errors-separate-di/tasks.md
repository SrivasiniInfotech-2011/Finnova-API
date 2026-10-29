# Implementation Tasks — Resolve Build Errors and Separate Repository/Service DI

**Scope:** backend composition-root refactor only. Copy each step into the indicated repository after reviewing the existing uncommitted work. These snippets do not change business logic, HTTP contracts, database schema, gateway routing, or the frontend.

## Task 1 — CREATE `Finnova.Service/DependencyInjection.cs`

Create the file at `e:\Finnova\Finnova-API\Finnova.Service\DependencyInjection.cs` with this complete content. This is the single service-layer registration entry point. It scans the existing service assembly once, keeps current lifetimes, and calls the existing blob-storage extension.

```csharp
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Finnova.Service.Assets.CodeGeneration;
using Finnova.Service.Auth;
using Finnova.Service.Customers.Validation;
using Finnova.Service.Document.Ports;
using Finnova.Service.GL.Ports;
using Finnova.Service.Storage;
using Finnova.Service.UserManagement.Abstractions;

namespace Finnova.Service;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the application/service layer for a Finnova API host.
    /// Repository and host authentication registrations remain in their own layers.
    /// </summary>
    public static IServiceCollection AddFinnovaServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var serviceAssembly = typeof(Finnova.Service.Auth.Commands.Login.LoginCommand).Assembly;

        // MediatR handlers and FluentValidation validators are all owned by Finnova.Service.
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(serviceAssembly));
        services.AddValidatorsFromAssembly(serviceAssembly);
        services.AddTransient(
            typeof(IPipelineBehavior<,>),
            typeof(Behaviors.ValidationBehavior<,>));

        // Options and shared service implementations.
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IPasswordPolicy, DefaultPasswordPolicy>();
        services.AddScoped<IAssetCodeGenerator, AssetCodeGenerator>();
        services.AddScoped<CustomerFieldApplicabilityEvaluator>();

        // Development GL ports. Hosts may override these registrations later with real adapters.
        services.AddScoped<IGLBalanceQueryPort, StubGLBalanceQueryPort>();
        services.AddScoped<IVoucherReferenceQueryPort, StubVoucherReferenceQueryPort>();
        services.AddScoped<IGLReferenceCheckPort, StubGLReferenceCheckPort>();
        services.AddScoped<ICompanyQueryPort, StubCompanyQueryPort>();
        services.AddScoped<IAccountingCalendarPort, StubAccountingCalendarPort>();
        services.AddScoped<IBranchQueryPort, StubBranchQueryPort>();
        services.AddScoped<ICurrencyQueryPort, StubCurrencyQueryPort>();

        // Document-layer ports. These are the existing lookup adapter and development stub.
        services.AddScoped<ILookupValidationPort, LookupValidationAdapter>();
        services.AddScoped<IDocumentUsageChecker, StubDocumentUsageChecker>();

        // Provider selection and singleton lifetime remain owned by the existing extension.
        services.AddBlobStorage(configuration);

        return services;
    }
}
```

## Task 2 — MODIFY `Finnova.Service/Finnova.Service.csproj`

In the existing first `ItemGroup` of package references, insert the following package reference immediately after the existing `FluentValidation` reference. Do not remove or change any existing package version.

```xml
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="11.11.0" />
```

The resulting adjacent lines must be:

```xml
    <PackageReference Include="FluentValidation" Version="11.11.0" />
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="11.11.0" />
```

This package is required because `AddValidatorsFromAssembly` now compiles inside `Finnova.Service` rather than inside each web host.

## Task 3 — MODIFY `Finnova.Repository/DependencyInjection.cs`

At the start of the existing `AddFinnovaRepository` method, immediately before `services.AddDbContext<FinnovaDbContext>`, insert the guard below. Keep every existing repository registration unchanged and in the same scoped lifetime.

Replace this existing method prefix:

```csharp
    public static IServiceCollection AddFinnovaRepository(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<FinnovaDbContext>(options =>
            options.UseSqlServer(connectionString));
```

with this complete prefix:

```csharp
    public static IServiceCollection AddFinnovaRepository(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<FinnovaDbContext>(options =>
            options.UseSqlServer(connectionString));
```

This is the repository boundary guard for direct callers. The host call sites in the next tasks handle a missing configuration key before this method is reached.

## Task 4 — MODIFY `Finnova.UAService/Program.cs`

### 4.1 Usings

Add this using with the other `Finnova` usings:

```csharp
using Finnova.Service;
```

Remove these now-unused usings if they are present:

```csharp
using Finnova.Service.Assets.CodeGeneration;
using Finnova.Service.Auth;
using Finnova.Service.Auth.Commands.Login;
using Finnova.Service.Storage;
using Finnova.Service.UserManagement.Abstractions;
using FluentValidation;
```

### 4.2 Remove the old service registration blocks

Delete the existing block beginning with `// MediatR - register handlers from the Service layer` through the `builder.Services.AddValidatorsFromAssembly(serviceAssembly);` call. Delete the direct token and asset registrations in the JWT settings section:

```csharp
// MediatR - register handlers from the Service layer
var serviceAssembly = typeof(LoginCommand).Assembly;
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(serviceAssembly));

// FluentValidation
builder.Services.AddValidatorsFromAssembly(serviceAssembly);
```

and:

```csharp
// JWT auth — settings + token service
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAssetCodeGenerator, AssetCodeGenerator>();
```

### 4.3 Replace the repository/storage section

Replace the existing block from `// Repository (EF Core + PostgreSQL)` through the direct password-policy registration immediately before `var app = builder.Build();`:

```csharp
// Repository (EF Core + PostgreSQL)
var connectionString = builder.Configuration.GetConnectionString("FinnovaConnection");
builder.Services.AddFinnovaRepository(connectionString);

// Blob storage (Azure / AWS) — provider selected via BlobStorage:Provider
builder.Services.AddBlobStorage(builder.Configuration);

// Health checks
builder.Services.AddHealthChecks();

// OpenAPI + Swagger
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<IPasswordPolicy,
    DefaultPasswordPolicy>();
```

with this complete block:

```csharp
// Repository + service layers. Both settings and registrations fail fast when required
// configuration is absent; no null-forgiving operator is used.
var connectionString = builder.Configuration.GetConnectionString("FinnovaConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'FinnovaConnection' is required.");
builder.Services.AddFinnovaRepository(connectionString);
builder.Services.AddFinnovaServices(builder.Configuration);

// Health checks
builder.Services.AddHealthChecks();

// OpenAPI + Swagger
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();
```

Do not change the existing JWT bearer configuration, authorization, middleware order, endpoint mapping, or health endpoint.

## Task 5 — MODIFY `Finnova.AccountsService/Program.cs`

### 5.1 Usings

Add:

```csharp
using Finnova.Service;
```

Remove these direct-registration usings if they are present and unused afterward:

```csharp
using Finnova.Service.Accounts.Commands.CreateAccount;
using Finnova.Service.Assets.CodeGeneration;
using Finnova.Service.Auth;
using Finnova.Service.Storage;
using Finnova.Service.UserManagement.Abstractions;
using FluentValidation;
```

### 5.2 Remove the old MediatR, validator, and application-service blocks

Delete the existing MediatR and FluentValidation block:

```csharp
// MediatR - register handlers from the Service layer
var serviceAssembly = typeof(CreateAccountCommand).Assembly;
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(serviceAssembly));

// FluentValidation
builder.Services.AddValidatorsFromAssembly(serviceAssembly);

// MediatR validation pipeline — runs FluentValidation before each handler (R2.6 ordering)
builder.Services.AddTransient(
    typeof(MediatR.IPipelineBehavior<,>),
    typeof(Finnova.Service.Behaviors.ValidationBehavior<,>));
```

Delete the direct application registrations from the auth section:

```csharp
// Auth token service — required because the shared Finnova.Service assembly (scanned by
// MediatR above) contains LoginCommandHandler, which depends on ITokenService.
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IPasswordPolicy, DefaultPasswordPolicy>();
builder.Services.AddScoped<IAssetCodeGenerator, AssetCodeGenerator>();
```

### 5.3 Replace repository, storage, and GL registrations

Replace the existing repository/storage/health beginning at `// Repository (EF Core + SQL Server)` and remove the direct GL stub block after Swagger. Keep the existing authorization policy block and Swagger security definition unchanged.

Use this repository/service/health block:

```csharp
// Repository + service layers.
var connectionString = builder.Configuration.GetConnectionString("FinnovaConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'FinnovaConnection' is required.");
builder.Services.AddFinnovaRepository(connectionString);
builder.Services.AddFinnovaServices(builder.Configuration);

// Health checks
builder.Services.AddHealthChecks();
```

Delete this old block because all of these registrations now live in `AddFinnovaServices`:

```csharp
// Blob storage (Azure / AWS) — provider selected via BlobStorage:Provider
builder.Services.AddBlobStorage(builder.Configuration);
```

Delete the complete direct GL port block:

```csharp
// GL dependency ports — dev stubs (design A2). Replace with real adapters before production.
builder.Services.AddScoped<Finnova.Service.GL.Ports.IGLBalanceQueryPort, Finnova.Service.GL.Ports.StubGLBalanceQueryPort>();
builder.Services.AddScoped<Finnova.Service.GL.Ports.IVoucherReferenceQueryPort, Finnova.Service.GL.Ports.StubVoucherReferenceQueryPort>();
builder.Services.AddScoped<Finnova.Service.GL.Ports.IGLReferenceCheckPort, Finnova.Service.GL.Ports.StubGLReferenceCheckPort>();
builder.Services.AddScoped<Finnova.Service.GL.Ports.ICompanyQueryPort, Finnova.Service.GL.Ports.StubCompanyQueryPort>();
builder.Services.AddScoped<Finnova.Service.GL.Ports.IAccountingCalendarPort, Finnova.Service.GL.Ports.StubAccountingCalendarPort>();
builder.Services.AddScoped<Finnova.Service.GL.Ports.IBranchQueryPort, Finnova.Service.GL.Ports.StubBranchQueryPort>();
builder.Services.AddScoped<Finnova.Service.GL.Ports.ICurrencyQueryPort, Finnova.Service.GL.Ports.StubCurrencyQueryPort>();
```

Do not change the Accounts-specific exception middleware, JWT configuration, authorization policies, or public `Program` shim.

## Task 6 — MODIFY `Finnova.SystemAdminService/Program.cs`

### 6.1 Usings

Add:

```csharp
using Finnova.Service;
```

Remove these direct-registration usings if they become unused:

```csharp
using Finnova.Service.Assets.CodeGeneration;
using Finnova.Service.Auth;
using Finnova.Service.Customers.Commands;
using Finnova.Service.Customers.Validation;
using Finnova.Service.UserManagement.Abstractions;
using FluentValidation;
```

### 6.2 Remove the repeated MediatR and application-service blocks

Delete the first MediatR/validator/pipeline block:

```csharp
// MediatR - register handlers from the Service layer (assembly marker pattern)
var serviceAssembly = typeof(Finnova.Service.Lookup.Commands.CreateLookupValue.CreateLookupValueCommand).Assembly;
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(serviceAssembly));

// FluentValidation
builder.Services.AddValidatorsFromAssembly(serviceAssembly);

// MediatR validation pipeline — runs FluentValidation before each handler
builder.Services.AddTransient(
    typeof(MediatR.IPipelineBehavior<,>),
    typeof(Finnova.Service.Behaviors.ValidationBehavior<,>));
```

Delete these direct token/asset registrations from the auth section:

```csharp
// Auth token service — required because the shared Finnova.Service assembly (scanned by
// MediatR above) contains LoginCommandHandler, which depends on ITokenService.
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAssetCodeGenerator, AssetCodeGenerator>();
```

### 6.3 Replace the repository and feature-registration blocks

Replace the existing block beginning at `// Repository (EF Core + SQL Server)` and ending after the second customer MediatR registration:

```csharp
// Repository (EF Core + SQL Server)
var connectionString = builder.Configuration.GetConnectionString("FinnovaConnection");
builder.Services.AddFinnovaRepository(connectionString);
builder.Services.AddScoped<CustomerFieldApplicabilityEvaluator>();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CreateCustomerCommand).Assembly));
```

with:

```csharp
// Repository + service layers.
var connectionString = builder.Configuration.GetConnectionString("FinnovaConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'FinnovaConnection' is required.");
builder.Services.AddFinnovaRepository(connectionString);
builder.Services.AddFinnovaServices(builder.Configuration);
```

Delete the later direct service blocks because their registrations now belong to the shared service extension:

```csharp
// GPS password policy (consumed by User Management). Dev default; swap for the real policy via DI.
builder.Services.AddScoped<IPasswordPolicy,
    DefaultPasswordPolicy>();
// Asset Master — pure, deterministic Asset Code generator (R3.2, R3.3).
builder.Services.AddScoped<IAssetCodeGenerator, AssetCodeGenerator>();
// Document Master ports. LookupValidationAdapter reads ACTIVE lookups; the usage checker is a
// dev stub (resolved assumption A3) to be replaced by the Customer Master adapter (FINNOVA-23).
builder.Services.AddScoped<Finnova.Service.Document.Ports.ILookupValidationPort,
    Finnova.Service.Document.Ports.LookupValidationAdapter>();
builder.Services.AddScoped<Finnova.Service.Document.Ports.IDocumentUsageChecker,
    Finnova.Service.Document.Ports.StubDocumentUsageChecker>();
```

Do not change the SystemAdmin JWT configuration, `SystemAdmin`/document authorization policies, exception middleware, endpoint mapping, health endpoint, or public `Program` shim. The customer evaluator remains available through `AddFinnovaServices`.

## Task 7 — CREATE `Finnova.Tests/Unit/DependencyInjectionRegistrationTests.cs`

Create this complete test file. It verifies registration descriptors without resolving `FinnovaDbContext` or contacting SQL Server.

```csharp
using Finnova.Repository;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;
using Finnova.Repository.Repositories;
using Finnova.Service;
using Finnova.Service.Assets.CodeGeneration;
using Finnova.Service.Auth;
using Finnova.Service.Behaviors;
using Finnova.Service.UserManagement.Abstractions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Finnova.Tests.Unit;

public sealed class DependencyInjectionRegistrationTests
{
    [Fact]
    public void Service_registration_contains_application_dependencies_with_expected_lifetimes()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddFinnovaServices(configuration);

        AssertDescriptor<ITokenService, TokenService>(services, ServiceLifetime.Scoped);
        AssertDescriptor<IPasswordPolicy, DefaultPasswordPolicy>(services, ServiceLifetime.Scoped);
        AssertDescriptor<IAssetCodeGenerator, AssetCodeGenerator>(services, ServiceLifetime.Scoped);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IPipelineBehavior<,>) &&
            descriptor.ImplementationType == typeof(ValidationBehavior<,>) &&
            descriptor.Lifetime == ServiceLifetime.Transient);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType.IsGenericType &&
            descriptor.ServiceType.GetGenericTypeDefinition() == typeof(IRequestHandler<,>) &&
            descriptor.ImplementationType is not null);
    }

    [Fact]
    public void Repository_registration_contains_context_and_repository_dependencies()
    {
        var services = new ServiceCollection();

        services.AddFinnovaRepository(
            "Server=(local);Database=Finnova;Trusted_Connection=True;TrustServerCertificate=True;");

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(FinnovaDbContext) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        AssertDescriptor<INationalityRepository, NationalityRepository>(services, ServiceLifetime.Scoped);
        AssertDescriptor<IUserManagementRepository, UserManagementRepository>(services, ServiceLifetime.Scoped);
    }

    [Fact]
    public void Repository_registration_rejects_blank_connection_strings()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
            services.AddFinnovaRepository("  "));
    }

    private static void AssertDescriptor<TService, TImplementation>(
        IServiceCollection services,
        ServiceLifetime lifetime)
    {
        var descriptor = Assert.Single(services.Where(x => x.ServiceType == typeof(TService)));
        Assert.Equal(typeof(TImplementation), descriptor.ImplementationType);
        Assert.Equal(lifetime, descriptor.Lifetime);
    }
}
```

If the test project already has a namespace convention for unit tests, keep the file path and change only the namespace to match that convention. Do not add a SQL Server connection or call `BuildServiceProvider` in this test.

## Commands to run later

The authoring workflow must not run these commands. After copying and applying the tasks, the user should run them from `e:\Finnova\Finnova-API`:

```powershell
dotnet build Finnova.Backend.slnx
dotnet test Finnova.Tests\Finnova.Tests.csproj
```

No EF migration command is needed because this change adds no entity, configuration, or schema change. No command is required in `Finnova-UI` because frontend impact is NONE.
