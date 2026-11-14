using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository;
using Finnova.Service.Assets.CodeGeneration;
using Finnova.Service.Auth;
using Finnova.Service.Customers;
using Finnova.Service.Customers.Commands;
using Finnova.Service.Customers.References;
using Finnova.Service.Customers.Validation;
using Finnova.Service.GL.Ports;
using Finnova.Service.IdentityVerification;
using Finnova.Service.IdentityVerification.Adapters;
using Finnova.Service.IdentityVerification.Correlation;
using Finnova.Service.IdentityVerification.External;
using Finnova.Service.IdentityVerification.Queries;
using Finnova.Service.IdentityVerification.Storage;
using Finnova.Service.IdentityVerification.Validation;
using Finnova.Service.IdentityVerification.Workflow;
using Finnova.Service.UserManagement.Abstractions;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Controllers. Serialize/bind enums by their string names (e.g. "Never", "Global") so the DCN
// contracts read naturally over the wire and match how the UI sends them.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

// MediatR - register handlers from the Service layer (assembly marker pattern)
var serviceAssembly = typeof(Finnova.Service.Lookup.Commands.CreateLookupValue.CreateLookupValueCommand).Assembly;
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(serviceAssembly));

// FluentValidation
builder.Services.AddValidatorsFromAssembly(serviceAssembly);

// MediatR validation pipeline â€” runs FluentValidation before each handler
builder.Services.AddTransient(
    typeof(MediatR.IPipelineBehavior<,>),
    typeof(Finnova.Service.Behaviors.ValidationBehavior<,>));

// Auth token service — required because the shared Finnova.Service assembly (scanned by
// MediatR above) contains LoginCommandHandler, which depends on ITokenService.
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAssetCodeGenerator, AssetCodeGenerator>();
// Customer master service graph (reference resolvers, validators, mappers, adapters) required
// by the Customer MediatR handlers scanned from the shared Finnova.Service assembly.
builder.Services.AddCustomerServices();
builder.Services.AddScoped<ILookupReferenceResolver, LookupReferenceResolver>();
builder.Services.AddScoped<INationalityReferenceResolver, NationalityReferenceResolver>();
builder.Services.AddScoped<IGLPostingGroupResolver, GLPostingGroupResolver>();
builder.Services.AddScoped<IDocumentDefinitionResolver, DocumentDefinitionResolver>();
builder.Services.AddScoped<IIndiaAddressControl, IndiaAddressControlDeferredAdapter>();
builder.Services.AddScoped<ICustomerMasterReferenceValidator, CustomerMasterReferenceValidator>();
builder.Services.AddScoped<IDocumentNumberGenerator, CustomerCodeGenerator>();
builder.Services.AddScoped<CustomerAggregateMapper>();
builder.Services.AddScoped<ICustomerAggregateMapper>(sp => sp.GetRequiredService<CustomerAggregateMapper>());
builder.Services.AddScoped<ICustomerProjectionMapper, CustomerProjectionMapper>();
builder.Services.AddScoped<CustomerChildMapper>();
builder.Services.AddScoped<ICustomerChildMapper>(sp => sp.GetRequiredService<CustomerChildMapper>());
builder.Services.AddScoped<AccountContractBoundary>();
builder.Services.AddScoped<IAccountContractBoundary>(sp => sp.GetRequiredService<AccountContractBoundary>());
// JVE submission only. The Python worker owns processing and canonical posting.
builder.Services.AddScoped<Finnova.Service.JVPosting.JvPostingSubmissionService>();
// JWT Bearer authentication (R7.6 â€” establishing the scheme)
var jwt = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,            // rejects expired tokens (R7.1)
            ValidateIssuerSigningKey = true,    // rejects bad signature (R7.1)
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwt["SigningKey"]!)),
            RoleClaimType = ClaimTypes.Role
        };
    });

// SystemAdmin authorization policy — role claim "SystemAdmin" (R7.2/7.3)
// Document Master maker/checker policies (R12.2/R12.3). Kept exclusive pending confirmation
// (design decision #6): SystemAdmin is NOT accepted as a super-role here.
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SystemAdmin", p => p.RequireRole("SystemAdmin"));
    options.AddPolicy("DOC_MASTER_MAKER", p => p.RequireRole("DOC_MASTER_MAKER"));
    options.AddPolicy("DOC_MASTER_CHECKER", p => p.RequireRole("DOC_MASTER_CHECKER"));
    options.AddPolicy("JveOperationsRead", policy => policy.RequireClaim("permission", "jve.operations.read"));
    options.AddPolicy("JveOperationsRecovery", policy => policy.RequireClaim("permission", "jve.operations.recovery"));
});

// Repository (EF Core + SQL Server)
var connectionString = builder.Configuration.GetConnectionString("FinnovaConnection");
builder.Services.AddFinnovaRepository(connectionString);
builder.Services.AddHttpContextAccessor();
builder.Services.Configure<IdentityVerificationOptions>(builder.Configuration.GetSection(IdentityVerificationOptions.SectionName));
builder.Services.AddHttpClient<ConfigurableComparisonHttpClient>();
builder.Services.AddScoped<IRequestContextAccessor, RequestContextAccessor>();
builder.Services.AddScoped<IProviderCallExecutor, ProviderCallExecutor>();
builder.Services.AddScoped<IVerificationWorkflowCoordinator, VerificationWorkflowCoordinator>();
builder.Services.AddScoped<DocumentInputValidator>();
builder.Services.AddScoped<ProtectedDocumentLifecycle>();
builder.Services.AddIdentityVerificationStorage(builder.Configuration);
builder.Services.AddScoped<IIdentityVerificationDocumentQuery, Finnova.Service.IdentityVerification.Queries.ProtectedDocumentQuery>();
builder.Services.AddScoped<Finnova.Service.JVPosting.JvPostingOperationsService>();
// Register only enabled provider adapters. Provider endpoints and credentials remain deployment configuration;
// an enabled operation with missing endpoint configuration returns an unavailable result and never succeeds.
var identityVerificationSection = builder.Configuration.GetSection(IdentityVerificationOptions.SectionName);
if (identityVerificationSection.GetValue<bool>("PanIdentifierValidation:Enabled"))
{
    builder.Services.AddScoped<IIdentifierValidationAdapter>(sp =>
    {
        var options = sp.GetRequiredService<IOptions<IdentityVerificationOptions>>().Value;
        return CreateIdentifierAdapter(VerificationOperationType.PanIdentifierValidation, options.PanIdentifierValidation, options.Retry);
    });
}

if (identityVerificationSection.GetValue<bool>("AadhaarIdentifierValidation:Enabled"))
{
    builder.Services.AddScoped<IIdentifierValidationAdapter>(sp =>
    {
        var options = sp.GetRequiredService<IOptions<IdentityVerificationOptions>>().Value;
        return CreateIdentifierAdapter(VerificationOperationType.AadhaarIdentifierValidation, options.AadhaarIdentifierValidation, options.Retry);
    });
}

if (identityVerificationSection.GetValue<bool>("PanNameVerification:Enabled"))
{
    builder.Services.AddScoped<IComparisonAdapter>(sp =>
    {
        var options = sp.GetRequiredService<IOptions<IdentityVerificationOptions>>().Value;
        return CreateComparisonAdapter(VerificationOperationType.PanNameVerification, options.PanNameVerification, options.Retry, sp);
    });
}

if (identityVerificationSection.GetValue<bool>("AadhaarAddressVerification:Enabled"))
{
    builder.Services.AddScoped<IComparisonAdapter>(sp =>
    {
        var options = sp.GetRequiredService<IOptions<IdentityVerificationOptions>>().Value;
        return CreateComparisonAdapter(VerificationOperationType.AadhaarAddressVerification, options.AadhaarAddressVerification, options.Retry, sp);
    });
}

builder.Services.AddScoped<IComparisonAdapterResolver, ConfiguredComparisonAdapterResolver>();

builder.Services.AddScoped<CustomerFieldApplicabilityEvaluator>();

static ConfiguredIdentifierAdapter CreateIdentifierAdapter(
    VerificationOperationType operation,
    ProviderOperationOptions provider,
    RetryOptions retry)
    => new(
        operation,
        provider.BaseAddress,
        provider.Path,
        provider.HttpMethod,
        provider.RequestMode,
        provider.IdentifierFieldName,
        Math.Max(1, retry.TimeoutSeconds),
        Math.Max(1, retry.MaxAttempts),
        Math.Max(0, retry.BaseDelayMilliseconds));

static ConfigurableComparisonHttpClient CreateComparisonAdapter(
    VerificationOperationType operation,
    ComparisonProviderOptions provider,
    RetryOptions retry,
    IServiceProvider services)
    => new(
        services.GetRequiredService<IHttpClientFactory>().CreateClient(),
        operation,
        provider,
        retry,
        services.GetRequiredService<ILogger<ConfigurableComparisonHttpClient>>());

// Health checks
builder.Services.AddHealthChecks();

// OpenAPI + Swagger (with Bearer security so Swagger UI supports auth)
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter the JWT bearer token."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", document),
            new List<string>()
        }
    });
});
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
// GL dependency ports — dev stubs. The shared Finnova.Service MediatR scan registers the GL
// handlers here too, so their ports must be in the container.
builder.Services.AddGLPorts();
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Middleware order is critical:
app.UseMiddleware<Finnova.SystemAdminService.Middleware.ExceptionHandlingMiddleware>(); // ERR-LKP-005 + validation -> ProblemDetails
app.UseAuthentication();   // must precede UseAuthorization
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// Testability shim: the top-level-statement Program class is implicitly internal, so
// WebApplicationFactory<Program> in Finnova.Tests cannot reference it. Declaring a public
// partial Program here exposes the generated entry-point type to the test assembly.
public partial class Program { }


internal sealed class ConfiguredComparisonAdapterResolver(
    IEnumerable<IComparisonAdapter> adapters,
    IOptions<IdentityVerificationOptions> options) : IComparisonAdapterResolver
{
    public IComparisonAdapter Resolve(VerificationOperationType operation, string adapterName)
    {
        var provider = operation switch
        {
            VerificationOperationType.PanNameVerification => options.Value.PanNameVerification,
            VerificationOperationType.AadhaarAddressVerification => options.Value.AadhaarAddressVerification,
            _ => throw new IdentityVerificationConfigurationException()
        };

        if (!provider.Enabled ||
            string.IsNullOrWhiteSpace(provider.AdapterName) ||
            string.IsNullOrWhiteSpace(provider.BaseAddress) ||
            string.IsNullOrWhiteSpace(provider.Path))
        {
            throw new IdentityVerificationConfigurationException();
        }

        var requestedName = adapterName?.Trim();
        var configuredProfile = provider.InputMapping.MappingProfile?.Trim();
        if (!string.IsNullOrWhiteSpace(requestedName) &&
            !string.Equals(requestedName, provider.AdapterName, StringComparison.Ordinal) &&
            !string.Equals(requestedName, configuredProfile, StringComparison.Ordinal))
        {
            throw new IdentityVerificationConfigurationException();
        }

        return adapters.SingleOrDefault(adapter => adapter.SupportedOperation == operation)
            ?? throw new IdentityVerificationConfigurationException();
    }
}
