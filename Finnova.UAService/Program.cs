using FluentValidation;
using Finnova.Service.Users.Commands.CreateUser;
using Finnova.Repository;

var builder = WebApplication.CreateBuilder(args);

// Controllers
builder.Services.AddControllers();

// MediatR - register handlers from the Service layer
var serviceAssembly = typeof(CreateUserCommand).Assembly;
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(serviceAssembly));

// FluentValidation
builder.Services.AddValidatorsFromAssembly(serviceAssembly);

// Repository (EF Core + PostgreSQL)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=localhost;Database=Finnova;Trusted_Connection=True;TrustServerCertificate=True";
builder.Services.AddFinnovaRepository(connectionString);

// Health checks
builder.Services.AddHealthChecks();

// OpenAPI
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
