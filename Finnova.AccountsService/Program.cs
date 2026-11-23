using Finnova.Repository;
using Finnova.Service;
using Finnova.Service.LoanAccounts;
using Finnova.Service.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Security.Claims;
using System.Text;
var builder = WebApplication.CreateBuilder(args);

// Controllers. Serialize/bind enums by their string names (e.g. "Active", "Control") so the
// GL contracts read naturally over the wire and match how the UI sends them.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter()));


builder.Services.Configure<AccountCreationKycOptions>(builder.Configuration.GetSection(AccountCreationKycOptions.SectionName));

// JWT Bearer authentication (R11.1/R11.5 â€” establishing the scheme before authorization)
var jwt = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,            // rejects expired tokens (R11.1)
            ValidateIssuerSigningKey = true,    // rejects bad signature (R11.1)
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwt["SigningKey"]!)),
            RoleClaimType = ClaimTypes.Role
        };
    });

// GL authorization policies (R11.2/11.3/11.4/11.6, R8.8). GLRead is satisfied by ANY GL role
// claim (GLMaker, GLChecker, SystemAdmin, or GLViewer) so a viewer-only user can read.
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("GLMaker", p => p.RequireRole("GLMaker"));
    options.AddPolicy("GLChecker", p => p.RequireRole("GLChecker"));
    options.AddPolicy("SystemAdmin", p => p.RequireRole("SystemAdmin"));
    options.AddPolicy("GLRead", p => p.RequireAssertion(ctx =>
        ctx.User.IsInRole("GLMaker") ||
        ctx.User.IsInRole("GLChecker") ||
        ctx.User.IsInRole("SystemAdmin") ||
        ctx.User.IsInRole("GLViewer")));
    options.AddPolicy("ApplicationProcessor", p => p.RequireRole("ApplicationProcessor"));
    options.AddPolicy("DemandRun", p => p.RequireAuthenticatedUser().RequireAssertion(ctx => ctx.User.IsInRole("Operations") || ctx.User.IsInRole("Accounts") || ctx.User.IsInRole("SystemAdmin")));
    options.AddPolicy("DemandOutputRead", p => p.RequireAuthenticatedUser().RequireAssertion(ctx => ctx.User.IsInRole("Operations") || ctx.User.IsInRole("Accounts") || ctx.User.IsInRole("BranchOperations") || ctx.User.IsInRole("SystemAdmin")));
    options.AddPolicy("DemandSpool", p => p.RequireAuthenticatedUser().RequireAssertion(ctx => ctx.User.IsInRole("Operations") || ctx.User.IsInRole("BranchOperations") || ctx.User.IsInRole("SystemAdmin") || ctx.User.IsInRole("Automizer")));
    options.AddPolicy("DemandPdfExport", p => p.RequireAuthenticatedUser().RequireAssertion(ctx => ctx.User.IsInRole("Operations") || ctx.User.IsInRole("Accounts") || ctx.User.IsInRole("BranchOperations") || ctx.User.IsInRole("SystemAdmin")));
    // Loan Accounts (application-account-creation). SystemAdmin is listed explicitly only for operations.
    options.AddPolicy("AccountsRead", p => p.RequireAssertion(ctx =>
        ctx.User.IsInRole("AccountsViewer") || ctx.User.IsInRole("ApplicationProcessor")));
    options.AddPolicy("KycApprover", p => p.RequireRole("KycApprover"));
    options.AddPolicy("AccountsOps", p => p.RequireAssertion(ctx =>
        ctx.User.IsInRole("AccountsOperator") || ctx.User.IsInRole("SystemAdmin")));
    options.AddPolicy("ReceiptAppropriationRead", p => p.RequireAuthenticatedUser().RequireAssertion(ctx => ctx.User.IsInRole("BranchCollectionOfficer") || ctx.User.IsInRole("SystemAdmin")));
    options.AddPolicy("ReceiptAppropriationConfirm", p => p.RequireAuthenticatedUser().RequireAssertion(ctx => ctx.User.IsInRole("BranchCollectionOfficer") || ctx.User.IsInRole("SystemAdmin")));
    // Receipt Processing (FINNOVA-61). Both policies accept the same roles; they are separate so reads can be widened later.
    options.AddPolicy("ReceiptEntry", p => p.RequireAuthenticatedUser().RequireAssertion(ctx =>
        ctx.User.IsInRole("Cashier") || ctx.User.IsInRole("BranchCollectionOfficer") || ctx.User.IsInRole("SystemAdmin")));
    options.AddPolicy("ReceiptRead", p => p.RequireAuthenticatedUser().RequireAssertion(ctx =>
        ctx.User.IsInRole("Cashier") || ctx.User.IsInRole("BranchCollectionOfficer") || ctx.User.IsInRole("SystemAdmin")));
});

// Repository (EF Core + SQL Server)
var connectionString = builder.Configuration.GetConnectionString("FinnovaConnection");
builder.Services.AddFinnovaRepository(connectionString);

// Service layer: core + Application Processing (this host serves the Application Processing handlers).
builder.Services.AddFinnovaServices(builder.Configuration, FinnovaServiceModules.ApplicationProcessing);
builder.Services.AddSingleton<Finnova.Service.DemandProcessing.Processing.DemandWorkQueue>();
builder.Services.AddSingleton<Finnova.Service.DemandProcessing.Ports.IDemandWorkQueue>(sp => sp.GetRequiredService<Finnova.Service.DemandProcessing.Processing.DemandWorkQueue>());
builder.Services.AddHostedService<Finnova.Service.DemandProcessing.Processing.DemandRunWorker>();

// Blob storage (Azure / AWS) â€” provider selected via BlobStorage:Provider
builder.Services.AddBlobStorage(builder.Configuration);

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
app.UseMiddleware<Finnova.AccountsService.Middlewares.ExceptionHandlingMiddleware>(); // GL exceptions -> ProblemDetails
app.UseAuthentication();   // must precede UseAuthorization (R11.5)
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// Testability shim: the top-level-statement Program class is implicitly internal, so
// WebApplicationFactory<Program> in Finnova.Tests cannot reference it. Declaring a public
// partial Program here exposes the generated entry-point type to the test assembly.
public partial class Program { }



