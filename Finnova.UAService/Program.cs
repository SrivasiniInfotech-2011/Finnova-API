using System.Security.Claims;
using System.Text;
using Finnova.Repository;
using Finnova.Service.Auth;
using Finnova.Service.Auth.Commands.Login;
using Finnova.Service.Storage;
using Finnova.Service.UserManagement.Abstractions;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Controllers
builder.Services.AddControllers();

// MediatR - register handlers from the Service layer
var serviceAssembly = typeof(LoginCommand).Assembly;
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(serviceAssembly));

// FluentValidation
builder.Services.AddValidatorsFromAssembly(serviceAssembly);

// JWT auth — settings + token service
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
builder.Services.AddScoped<ITokenService, TokenService>();

// JWT Bearer authentication — mirrors the SystemAdmin host so authorized endpoints (e.g. the
// per-screen permissions endpoint) can validate the token this host issues.
var jwt = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwt["SigningKey"]!)),
            RoleClaimType = ClaimTypes.Role
        };
    });
builder.Services.AddAuthorization();

// Repository (EF Core + PostgreSQL)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=localhost;Database=Finnova;Trusted_Connection=True;TrustServerCertificate=True";
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
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();   // must precede UseAuthorization
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
