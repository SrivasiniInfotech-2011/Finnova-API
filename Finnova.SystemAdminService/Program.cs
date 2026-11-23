using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository;
using Finnova.Service;
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
using Finnova.Service.LoanAccounts.Kyc;
using Finnova.SystemAdminService.ServiceAuth;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Controllers. Serialize/bind enums by their string names (e.g. "Never", "Global") so the DCN
// contracts read naturally over the wire and match how the UI sends them.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

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
    options.AddPolicy("ServiceDncIssue", policy => policy.RequireClaim("permission", "svc.dcn.issue"));
    options.AddPolicy("ServiceKycVerify", policy => policy.RequireClaim("permission", "svc.kyc.verify"));
    options.AddPolicy("AppropriationLogicRead", policy => policy.RequireAuthenticatedUser().RequireRole("SystemAdmin"));
    options.AddPolicy("AppropriationLogicManage", policy => policy.RequireAuthenticatedUser().RequireRole("SystemAdmin"));
    options.AddPolicy("AppropriationLogicResolve", policy => policy.RequireAuthenticatedUser().RequireAssertion(context => context.User.IsInRole("SystemAdmin") || context.User.HasClaim("scope", "appropriation-logic.resolve")));
});

// Repository (EF Core + SQL Server)
var connectionString = builder.Configuration.GetConnectionString("FinnovaConnection");
builder.Services.AddFinnovaRepository(connectionString);

// Service layer: core + Identity Verification (this host serves the Identity Verification handlers).
builder.Services.AddFinnovaServices(builder.Configuration, FinnovaServiceModules.IdentityVerification);
builder.Services.AddScoped<Finnova.Service.JVPosting.JvPostingOperationsService>();
// Internal service endpoints (application-account-creation): service token, DNC issuance, KYC verify.
builder.Services.Configure<ServiceAuthOptions>(builder.Configuration.GetSection(ServiceAuthOptions.SectionName));
builder.Services.AddScoped<ServiceTokenIssuer>();
builder.Services.AddHostedService<SystemUserSeeder>();
builder.Services.AddScoped<IKycAggregatorProvider, NullKycAggregatorProvider>();   // swap for a real adapter later (R7.10)
builder.Services.AddScoped<IKycVerificationFacade, KycVerificationFacade>();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddFixedWindowLimiter("service-token", l => { l.PermitLimit = 30; l.Window = TimeSpan.FromMinutes(1); l.QueueLimit = 0; });
});


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
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Middleware order is critical:
app.UseMiddleware<InternalEndpointGuardMiddleware>();   // 404 for /internal/** when forwarded headers are present
app.UseMiddleware<Finnova.SystemAdminService.Middleware.ExceptionHandlingMiddleware>(); // ERR-LKP-005 + validation -> ProblemDetails
app.UseRateLimiter();
app.UseAuthentication();   // must precede UseAuthorization
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// Testability shim: the top-level-statement Program class is implicitly internal, so
// WebApplicationFactory<Program> in Finnova.Tests cannot reference it. Declaring a public
// partial Program here exposes the generated entry-point type to the test assembly.
public partial class Program { }
