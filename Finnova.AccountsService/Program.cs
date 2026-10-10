using Finnova.Repository;
using Finnova.Service.Accounts.Commands.CreateAccount;
using Finnova.Service.Assets.CodeGeneration;
using Finnova.Service.Auth;
using Finnova.Service.Storage;
using Finnova.Service.UserManagement.Abstractions;
using FluentValidation;

var builder = WebApplication.CreateBuilder(args);

// Controllers
builder.Services.AddControllers();

// MediatR - register handlers from the Service layer
var serviceAssembly = typeof(CreateAccountCommand).Assembly;
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(serviceAssembly));

// FluentValidation
builder.Services.AddValidatorsFromAssembly(serviceAssembly);

// Auth token service — required because the shared Finnova.Service assembly (scanned by
// MediatR above) contains LoginCommandHandler, which depends on ITokenService.
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IPasswordPolicy,
    DefaultPasswordPolicy>();
builder.Services.AddScoped<IAssetCodeGenerator, AssetCodeGenerator>();
// Repository (EF Core + PostgreSQL)
var connectionString = builder.Configuration.GetConnectionString("FinnovaConnection");
builder.Services.AddFinnovaRepository(connectionString!);

// Blob storage (Azure / AWS) â€” provider selected via BlobStorage:Provider
builder.Services.AddBlobStorage(builder.Configuration);

// Health checks
builder.Services.AddHealthChecks();

// OpenAPI + Swagger
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

