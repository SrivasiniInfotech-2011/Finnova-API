using FluentValidation;
using Finnova.Service.Accounts.Commands.CreateAccount;
using Finnova.Service.Storage;
using Finnova.Repository;

var builder = WebApplication.CreateBuilder(args);

// Controllers
builder.Services.AddControllers();

// MediatR - register handlers from the Service layer
var serviceAssembly = typeof(CreateAccountCommand).Assembly;
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(serviceAssembly));

// FluentValidation
builder.Services.AddValidatorsFromAssembly(serviceAssembly);

// Repository (EF Core + PostgreSQL)
var connectionString = builder.Configuration.GetConnectionString("FinnovaConnection");
builder.Services.AddFinnovaRepository(connectionString!);

// Blob storage (Azure / AWS) — provider selected via BlobStorage:Provider
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
