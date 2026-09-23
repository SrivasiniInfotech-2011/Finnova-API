using System.Security.Claims;
using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Finnova.Repository;

var builder = WebApplication.CreateBuilder(args);

// Controllers
builder.Services.AddControllers();

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

// JWT Bearer authentication (R7.6 — establishing the scheme)
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
builder.Services.AddAuthorization(options =>
    options.AddPolicy("SystemAdmin", p => p.RequireRole("SystemAdmin")));

// Repository (EF Core + SQL Server)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=localhost;Database=Finnova;Trusted_Connection=True;TrustServerCertificate=True";
builder.Services.AddFinnovaRepository(connectionString);

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