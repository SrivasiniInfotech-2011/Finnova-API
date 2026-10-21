using Finnova.Repository;
using Finnova.Service.Accounts.Commands.CreateAccount;
using Finnova.Service.Assets.CodeGeneration;
using Finnova.Service.Auth;
using Finnova.Service.Storage;
using Finnova.Service.UserManagement.Abstractions;
using FluentValidation;
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

// MediatR - register handlers from the Service layer
var serviceAssembly = typeof(CreateAccountCommand).Assembly;
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(serviceAssembly));

// FluentValidation
builder.Services.AddValidatorsFromAssembly(serviceAssembly);

// MediatR validation pipeline â€” runs FluentValidation before each handler (R2.6 ordering)
builder.Services.AddTransient(
    typeof(MediatR.IPipelineBehavior<,>),
    typeof(Finnova.Service.Behaviors.ValidationBehavior<,>));

// Auth token service â€” required because the shared Finnova.Service assembly (scanned by
// MediatR above) contains LoginCommandHandler, which depends on ITokenService.
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IPasswordPolicy, DefaultPasswordPolicy>();
builder.Services.AddScoped<IAssetCodeGenerator, AssetCodeGenerator>();

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
});

// Repository (EF Core + SQL Server)
var connectionString = builder.Configuration.GetConnectionString("FinnovaConnection");
builder.Services.AddFinnovaRepository(connectionString!);

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
// GL dependency ports â€” dev stubs (design A2). Replace with real adapters before production.
builder.Services.AddScoped<Finnova.Service.GL.Ports.IGLBalanceQueryPort, Finnova.Service.GL.Ports.StubGLBalanceQueryPort>();
builder.Services.AddScoped<Finnova.Service.GL.Ports.IVoucherReferenceQueryPort, Finnova.Service.GL.Ports.StubVoucherReferenceQueryPort>();
builder.Services.AddScoped<Finnova.Service.GL.Ports.IGLReferenceCheckPort, Finnova.Service.GL.Ports.StubGLReferenceCheckPort>();
builder.Services.AddScoped<Finnova.Service.GL.Ports.ICompanyQueryPort, Finnova.Service.GL.Ports.StubCompanyQueryPort>();
builder.Services.AddScoped<Finnova.Service.GL.Ports.IAccountingCalendarPort, Finnova.Service.GL.Ports.StubAccountingCalendarPort>();
builder.Services.AddScoped<Finnova.Service.GL.Ports.IBranchQueryPort, Finnova.Service.GL.Ports.StubBranchQueryPort>();
builder.Services.AddScoped<Finnova.Service.GL.Ports.ICurrencyQueryPort, Finnova.Service.GL.Ports.StubCurrencyQueryPort>();
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


