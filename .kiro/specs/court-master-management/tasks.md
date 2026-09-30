# Tasks — Court Master Management (FINNOVA-13)

Copy-paste each task in order. **CREATE** = new file (paste the whole block). **MODIFY** = edit an
existing file (apply the shown snippet at the indicated place). Backend root:
`e:\Finnova\Finnova-API`. Frontend root: `E:\Finnova\Finnova-UI\Finnova-UI`.

After backend tasks 1–7: run the migration (task 4 command) and `dotnet build Finnova.Backend.slnx`.
After all backend tasks: `dotnet test Finnova.Tests\Finnova.Tests.csproj`.
After frontend tasks: `npm run build` and `npm test` in the Finnova-UI repo.

---

## Task 1 — Backend domain: enums + entities

### CREATE `Finnova.Models/Domain/Enums/CourtType.cs`

```csharp
namespace Finnova.Models.Domain.Enums;

/// <summary>The category of court (FINNOVA-13 R1.5).</summary>
public enum CourtType
{
    Supreme = 0,
    High = 1,
    District = 2,
    Tribunal = 3,
    Other = 4
}
```

### CREATE `Finnova.Models/Domain/Enums/CourtAuditAction.cs`

```csharp
namespace Finnova.Models.Domain.Enums;

/// <summary>Discriminates the configuration mutation an audit entry records (FINNOVA-13 R5).</summary>
public enum CourtAuditAction
{
    Create = 0,
    Update = 1
}
```

### CREATE `Finnova.Models/Domain/Entities/Court.cs`

```csharp
using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

/// <summary>
/// A court in the Court Master (FINNOVA-13). Flat master with reference attributes. India-only
/// platform: one English Name.
/// </summary>
public class Court
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Code { get; set; } = string.Empty;         // max 20, unique (case-insensitive, trimmed)
    public string Name { get; set; } = string.Empty;         // max 150, English
    public CourtType CourtType { get; set; }                 // Supreme/High/District/Tribunal/Other
    public string Jurisdiction { get; set; } = string.Empty; // max 100
    public string Location { get; set; } = string.Empty;     // max 100

    public bool IsActive { get; set; } = true;               // default true (R1.2)

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

### CREATE `Finnova.Models/Domain/Entities/CourtAuditEntry.cs`

```csharp
using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

/// <summary>
/// Immutable audit record capturing one configuration change to a court (FINNOVA-13 R5). Written on
/// create and update (including activate/deactivate) within the same unit of work. Never updated or
/// deleted. The editable surface is multi-field, so before/after is captured as a compact JSON
/// snapshot plus a human-readable summary.
/// </summary>
public class CourtAuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CourtId { get; set; }
    public CourtAuditAction Action { get; set; }             // Create | Update

    public string? OldValues { get; set; }                   // JSON snapshot of changed fields (null on Create)
    public string NewValues { get; set; } = string.Empty;    // JSON snapshot of resulting fields
    public string Summary { get; set; } = string.Empty;      // human-readable change summary

    public string ChangedBy { get; set; } = string.Empty;    // acting admin id from JWT
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
}
```

---

## Task 2 — Backend domain: exceptions

### CREATE `Finnova.Models/Domain/Exceptions/CourtNotFoundException.cs`

```csharp
namespace Finnova.Models.Domain.Exceptions;

/// <summary>Thrown when an operation targets a court that does not exist. Maps to ERR-CRT-404.</summary>
public class CourtNotFoundException : Exception
{
    public CourtNotFoundException(Guid id)
        : base($"Court '{id}' was not found.")
    {
    }
}
```

### CREATE `Finnova.Models/Domain/Exceptions/CourtDuplicateCodeException.cs`

```csharp
namespace Finnova.Models.Domain.Exceptions;

/// <summary>
/// Thrown when a court create is attempted with a code that already exists. Maps to ERR-CRT-409.
/// </summary>
public class CourtDuplicateCodeException : Exception
{
    public const string ErrorCode = "ERR-CRT-409";

    public CourtDuplicateCodeException()
        : base("Court code must be unique")
    {
    }
}
```

---

## Task 3 — Backend contracts

### CREATE `Finnova.Models/Contracts/Courts/CreateCourtRequest.cs`

```csharp
using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Contracts.Courts;

/// <summary>Request to create a court. IsActive is nullable so the service defaults it to true (R1.2).</summary>
public record CreateCourtRequest(
    string Code,
    string Name,
    CourtType CourtType,
    string Jurisdiction,
    string Location,
    bool? IsActive);
```

### CREATE `Finnova.Models/Contracts/Courts/UpdateCourtRequest.cs`

```csharp
using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Contracts.Courts;

/// <summary>Request to update a court's editable fields. Code is immutable after creation (R2.3, R4.2).</summary>
public record UpdateCourtRequest(
    string Name,
    CourtType CourtType,
    string Jurisdiction,
    string Location,
    bool IsActive);
```

### CREATE `Finnova.Models/Contracts/Courts/CourtResponse.cs`

```csharp
using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Contracts.Courts;

public record CourtResponse(
    Guid Id,
    string Code,
    string Name,
    CourtType CourtType,
    string Jurisdiction,
    string Location,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);
```

### CREATE `Finnova.Models/Contracts/Courts/CourtAuditEntryResponse.cs`

```csharp
namespace Finnova.Models.Contracts.Courts;

public record CourtAuditEntryResponse(
    Guid Id,
    Guid CourtId,
    string Action,          // "Create" | "Update"
    string? OldValues,
    string NewValues,
    string Summary,
    string ChangedBy,
    DateTime ChangedAtUtc);
```

---

## Task 4 — Backend EF configuration + DbContext + migration

### CREATE `Finnova.Repository/Configuration/CourtConfiguration.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class CourtConfiguration : IEntityTypeConfiguration<Court>
{
    public void Configure(EntityTypeBuilder<Court> builder)
    {
        builder.ToTable("courts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(150);
        builder.Property(x => x.CourtType).IsRequired();      // stored as int
        builder.Property(x => x.Jurisdiction).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Location).IsRequired().HasMaxLength(100);
        builder.Property(x => x.IsActive).IsRequired();

        // Code uniqueness (R2.1/2.2). Default CI collation backs the case-insensitive rule; codes
        // are trimmed by the handler so stored values are canonical.
        builder.HasIndex(x => x.Code).IsUnique();
        // Query/order by Name (R3.7).
        builder.HasIndex(x => x.Name);
    }
}
```

### CREATE `Finnova.Repository/Configuration/CourtAuditEntryConfiguration.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Configuration;

public class CourtAuditEntryConfiguration : IEntityTypeConfiguration<CourtAuditEntry>
{
    public void Configure(EntityTypeBuilder<CourtAuditEntry> builder)
    {
        builder.ToTable("court_audit_entries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CourtId).IsRequired();
        builder.Property(x => x.Action).IsRequired();          // stored as int
        builder.Property(x => x.OldValues);                    // nullable JSON text
        builder.Property(x => x.NewValues).IsRequired();
        builder.Property(x => x.Summary).IsRequired().HasMaxLength(400);
        builder.Property(x => x.ChangedBy).IsRequired().HasMaxLength(200);
        builder.Property(x => x.ChangedAtUtc).IsRequired();
        // Read path: entries for a court ordered by time desc then id desc (R5.5).
        builder.HasIndex(x => new { x.CourtId, x.ChangedAtUtc });
    }
}
```

### MODIFY `Finnova.Repository/Context/FinnovaDbContext.cs`

Add these two DbSets alongside the existing ones (e.g. after the `NumberingScheme` DbSets):

```csharp
public DbSet<Court> Courts => Set<Court>();
public DbSet<CourtAuditEntry> CourtAuditEntries => Set<CourtAuditEntry>();
```

### Migration command (run from `e:\Finnova\Finnova-API`)

```
dotnet ef migrations add AddCourtAndAudit --project Finnova.Repository --startup-project Finnova.SystemAdminService
```

---

## Task 5 — Backend repositories + interfaces + DI

### CREATE `Finnova.Repository/Interfaces/ICourtRepository.cs`

```csharp
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Interfaces;

public interface ICourtRepository : IRepository<Court>
{
    /// <summary>
    /// Paged search over Code / Name / Jurisdiction (substring, case-insensitive; blank term = no
    /// filter). Ordered by Name asc, then Code asc (R3.7). Returns the page and the total count.
    /// </summary>
    Task<(List<Court> Items, int Total)> GetPagedAsync(
        string? searchTerm, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Case-insensitive, trimmed uniqueness check on Code (R2.1/2.2).</summary>
    Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default);
}
```

### CREATE `Finnova.Repository/Interfaces/ICourtAuditRepository.cs`

```csharp
using Finnova.Models.Domain.Entities;

namespace Finnova.Repository.Interfaces;

public interface ICourtAuditRepository : IRepository<CourtAuditEntry>
{
    /// <summary>
    /// Audit entries for a court, ordered ChangedAtUtc desc then Id desc (R5.5). Missing id yields
    /// an empty list (R5.6).
    /// </summary>
    Task<List<CourtAuditEntry>> GetByCourtIdAsync(Guid courtId, CancellationToken ct = default);
}
```

### CREATE `Finnova.Repository/Repositories/CourtRepository.cs`

```csharp
using Finnova.Models.Domain.Entities;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Finnova.Repository.Repositories;

public class CourtRepository(FinnovaDbContext finnovaDbContext)
    : RepositoryBase<Court>(finnovaDbContext), ICourtRepository
{
    public async Task<(List<Court> Items, int Total)> GetPagedAsync(
        string? searchTerm, int page, int pageSize, CancellationToken ct = default)
    {
        var query = DbSet.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(x =>
                x.Code.Contains(term) || x.Name.Contains(term) || x.Jurisdiction.Contains(term));
        }
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(x => x.Name).ThenBy(x => x.Code)   // R3.7
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    public async Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default)
    {
        var c = code.Trim();
        return await DbSet.AsNoTracking().AnyAsync(
            x => x.Code == c && (excludeId == null || x.Id != excludeId), ct);
    }
}
```

### CREATE `Finnova.Repository/Repositories/CourtAuditRepository.cs`

```csharp
using Finnova.Models.Domain.Entities;
using Finnova.Repository.Context;
using Finnova.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Finnova.Repository.Repositories;

public class CourtAuditRepository(FinnovaDbContext finnovaDbContext)
    : RepositoryBase<CourtAuditEntry>(finnovaDbContext), ICourtAuditRepository
{
    public async Task<List<CourtAuditEntry>> GetByCourtIdAsync(Guid courtId, CancellationToken ct = default)
    {
        return await DbSet.AsNoTracking()
            .Where(x => x.CourtId == courtId)
            .OrderByDescending(x => x.ChangedAtUtc).ThenByDescending(x => x.Id)   // R5.5
            .ToListAsync(ct);   // empty when none (R5.6)
    }
}
```

### MODIFY `Finnova.Repository/DependencyInjection.cs`

Add these registrations next to the other `AddScoped` repository lines (before `return services;`):

```csharp
services.AddScoped<ICourtRepository, CourtRepository>();
services.AddScoped<ICourtAuditRepository, CourtAuditRepository>();
```

---

## Task 6 — Backend service CQRS slice

### CREATE `Finnova.Service/Court/Internal/CourtAuditSnapshot.cs`

```csharp
using System.Text.Json;
using Finnova.Models.Domain.Entities;

namespace Finnova.Service.Court.Internal;

/// <summary>Builds compact JSON snapshots of a court's editable fields for audit entries (R5).</summary>
public static class CourtAuditSnapshot
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    public static string Editable(Court c) => JsonSerializer.Serialize(new
    {
        c.Name,
        CourtType = c.CourtType.ToString(),
        c.Jurisdiction,
        c.Location,
        c.IsActive,
    }, Options);
}
```

### CREATE `Finnova.Service/Court/Commands/CreateCourt/CreateCourtCommand.cs`

```csharp
using Finnova.Models.Contracts.Courts;
using Finnova.Models.Domain.Enums;
using MediatR;

namespace Finnova.Service.Court.Commands.CreateCourt;

public record CreateCourtCommand(
    string Code,
    string Name,
    CourtType CourtType,
    string Jurisdiction,
    string Location,
    bool? IsActive,
    string ActingAdmin) : IRequest<CourtResponse>;
```

### CREATE `Finnova.Service/Court/Commands/CreateCourt/CreateCourtCommandValidator.cs`

```csharp
using FluentValidation;

namespace Finnova.Service.Court.Commands.CreateCourt;

public class CreateCourtCommandValidator : AbstractValidator<CreateCourtCommand>
{
    public CreateCourtCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Court code is required.")
            .MaximumLength(20).WithMessage("Court code must not exceed 20 characters.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(150).WithMessage("Name must not exceed 150 characters.");

        RuleFor(x => x.CourtType).IsInEnum().WithMessage("Court type is invalid.");

        RuleFor(x => x.Jurisdiction)
            .NotEmpty().WithMessage("Jurisdiction is required.")
            .MaximumLength(100).WithMessage("Jurisdiction must not exceed 100 characters.");

        RuleFor(x => x.Location)
            .NotEmpty().WithMessage("Location is required.")
            .MaximumLength(100).WithMessage("Location must not exceed 100 characters.");
    }
}
```

### CREATE `Finnova.Service/Court/Commands/CreateCourt/CreateCourtCommandHandler.cs`

```csharp
using Finnova.Models.Contracts.Courts;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Court.Internal;
using Finnova.Service.Mappers;
using MediatR;

namespace Finnova.Service.Court.Commands.CreateCourt;

public class CreateCourtCommandHandler : IRequestHandler<CreateCourtCommand, CourtResponse>
{
    private readonly ICourtRepository _repository;
    private readonly ICourtAuditRepository _auditRepository;

    public CreateCourtCommandHandler(ICourtRepository repository, ICourtAuditRepository auditRepository)
    {
        _repository = repository;
        _auditRepository = auditRepository;
    }

    public async Task<CourtResponse> Handle(CreateCourtCommand request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim();

        if (await _repository.ExistsByCodeAsync(code, null, cancellationToken))
            throw new CourtDuplicateCodeException();   // R2.2

        var entity = new Finnova.Models.Domain.Entities.Court
        {
            Code = code,
            Name = request.Name.Trim(),
            CourtType = request.CourtType,
            Jurisdiction = request.Jurisdiction.Trim(),
            Location = request.Location.Trim(),
            IsActive = request.IsActive ?? true,   // R1.2
        };

        await _repository.AddAsync(entity, cancellationToken);

        await _auditRepository.AddAsync(new CourtAuditEntry
        {
            CourtId = entity.Id,
            Action = CourtAuditAction.Create,
            OldValues = null,
            NewValues = CourtAuditSnapshot.Editable(entity),
            Summary = $"Created court '{entity.Code}'.",
            ChangedBy = request.ActingAdmin,
            ChangedAtUtc = DateTime.UtcNow,
        }, cancellationToken);

        return entity.ToResponse();
    }
}
```

### CREATE `Finnova.Service/Court/Commands/UpdateCourt/UpdateCourtCommand.cs`

```csharp
using Finnova.Models.Contracts.Courts;
using Finnova.Models.Domain.Enums;
using MediatR;

namespace Finnova.Service.Court.Commands.UpdateCourt;

public record UpdateCourtCommand(
    Guid Id,
    string Name,
    CourtType CourtType,
    string Jurisdiction,
    string Location,
    bool IsActive,
    string ActingAdmin) : IRequest<CourtResponse>;
```

### CREATE `Finnova.Service/Court/Commands/UpdateCourt/UpdateCourtCommandValidator.cs`

```csharp
using FluentValidation;

namespace Finnova.Service.Court.Commands.UpdateCourt;

public class UpdateCourtCommandValidator : AbstractValidator<UpdateCourtCommand>
{
    public UpdateCourtCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Id is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(150).WithMessage("Name must not exceed 150 characters.");

        RuleFor(x => x.CourtType).IsInEnum().WithMessage("Court type is invalid.");

        RuleFor(x => x.Jurisdiction)
            .NotEmpty().WithMessage("Jurisdiction is required.")
            .MaximumLength(100).WithMessage("Jurisdiction must not exceed 100 characters.");

        RuleFor(x => x.Location)
            .NotEmpty().WithMessage("Location is required.")
            .MaximumLength(100).WithMessage("Location must not exceed 100 characters.");
    }
}
```

### CREATE `Finnova.Service/Court/Commands/UpdateCourt/UpdateCourtCommandHandler.cs`

```csharp
using Finnova.Models.Contracts.Courts;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Court.Internal;
using Finnova.Service.Mappers;
using MediatR;

namespace Finnova.Service.Court.Commands.UpdateCourt;

public class UpdateCourtCommandHandler : IRequestHandler<UpdateCourtCommand, CourtResponse>
{
    private readonly ICourtRepository _repository;
    private readonly ICourtAuditRepository _auditRepository;

    public UpdateCourtCommandHandler(ICourtRepository repository, ICourtAuditRepository auditRepository)
    {
        _repository = repository;
        _auditRepository = auditRepository;
    }

    public async Task<CourtResponse> Handle(UpdateCourtCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new CourtNotFoundException(request.Id);   // R4.4

        var newName = request.Name.Trim();
        var newJurisdiction = request.Jurisdiction.Trim();
        var newLocation = request.Location.Trim();

        // No-op detection: all editable fields equal current (R4.5).
        var unchanged =
            entity.Name == newName &&
            entity.CourtType == request.CourtType &&
            entity.Jurisdiction == newJurisdiction &&
            entity.Location == newLocation &&
            entity.IsActive == request.IsActive;
        if (unchanged)
            return entity.ToResponse();

        var before = CourtAuditSnapshot.Editable(entity);
        var changes = new List<string>();
        if (entity.Name != newName) changes.Add("Name");
        if (entity.CourtType != request.CourtType) changes.Add($"CourtType {entity.CourtType}->{request.CourtType}");
        if (entity.Jurisdiction != newJurisdiction) changes.Add("Jurisdiction");
        if (entity.Location != newLocation) changes.Add("Location");
        if (entity.IsActive != request.IsActive) changes.Add($"IsActive {entity.IsActive}->{request.IsActive}");

        entity.Name = newName;
        entity.CourtType = request.CourtType;
        entity.Jurisdiction = newJurisdiction;
        entity.Location = newLocation;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(entity, cancellationToken);

        await _auditRepository.AddAsync(new CourtAuditEntry
        {
            CourtId = entity.Id,
            Action = CourtAuditAction.Update,
            OldValues = before,
            NewValues = CourtAuditSnapshot.Editable(entity),
            Summary = "Updated: " + string.Join(", ", changes),
            ChangedBy = request.ActingAdmin,
            ChangedAtUtc = DateTime.UtcNow,
        }, cancellationToken);

        return entity.ToResponse();
    }
}
```

### CREATE `Finnova.Service/Court/Commands/SetCourtActive/SetCourtActiveCommand.cs`

```csharp
using Finnova.Models.Contracts.Courts;
using MediatR;

namespace Finnova.Service.Court.Commands.SetCourtActive;

/// <summary>Activates or deactivates a court (soft retire) (R6).</summary>
public record SetCourtActiveCommand(Guid Id, bool IsActive, string ActingAdmin) : IRequest<CourtResponse>;
```

### CREATE `Finnova.Service/Court/Commands/SetCourtActive/SetCourtActiveCommandHandler.cs`

```csharp
using Finnova.Models.Contracts.Courts;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Court.Internal;
using Finnova.Service.Mappers;
using MediatR;

namespace Finnova.Service.Court.Commands.SetCourtActive;

public class SetCourtActiveCommandHandler : IRequestHandler<SetCourtActiveCommand, CourtResponse>
{
    private readonly ICourtRepository _repository;
    private readonly ICourtAuditRepository _auditRepository;

    public SetCourtActiveCommandHandler(ICourtRepository repository, ICourtAuditRepository auditRepository)
    {
        _repository = repository;
        _auditRepository = auditRepository;
    }

    public async Task<CourtResponse> Handle(SetCourtActiveCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new CourtNotFoundException(request.Id);   // R6.3

        if (entity.IsActive == request.IsActive)
            return entity.ToResponse();   // no-op, no audit

        var before = CourtAuditSnapshot.Editable(entity);
        var oldActive = entity.IsActive;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(entity, cancellationToken);

        await _auditRepository.AddAsync(new CourtAuditEntry
        {
            CourtId = entity.Id,
            Action = CourtAuditAction.Update,
            OldValues = before,
            NewValues = CourtAuditSnapshot.Editable(entity),
            Summary = $"IsActive {oldActive}->{request.IsActive}",
            ChangedBy = request.ActingAdmin,
            ChangedAtUtc = DateTime.UtcNow,
        }, cancellationToken);

        return entity.ToResponse();
    }
}
```

### CREATE `Finnova.Service/Court/Queries/GetCourtsPaged/GetCourtsPagedQuery.cs`

```csharp
using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.Courts;
using MediatR;

namespace Finnova.Service.Court.Queries.GetCourtsPaged;

public record GetCourtsPagedQuery(string? SearchTerm, int Page = 1, int PageSize = 20)
    : IRequest<PaginatedResponse<CourtResponse>>;
```

### CREATE `Finnova.Service/Court/Queries/GetCourtsPaged/GetCourtsPagedQueryValidator.cs`

```csharp
using FluentValidation;

namespace Finnova.Service.Court.Queries.GetCourtsPaged;

public class GetCourtsPagedQueryValidator : AbstractValidator<GetCourtsPagedQuery>
{
    public GetCourtsPagedQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);           // R3.5
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);      // R3.5
    }
}
```

### CREATE `Finnova.Service/Court/Queries/GetCourtsPaged/GetCourtsPagedQueryHandler.cs`

```csharp
using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.Courts;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;
using MediatR;

namespace Finnova.Service.Court.Queries.GetCourtsPaged;

public class GetCourtsPagedQueryHandler
    : IRequestHandler<GetCourtsPagedQuery, PaginatedResponse<CourtResponse>>
{
    private readonly ICourtRepository _repository;

    public GetCourtsPagedQueryHandler(ICourtRepository repository) => _repository = repository;

    public async Task<PaginatedResponse<CourtResponse>> Handle(
        GetCourtsPagedQuery request, CancellationToken cancellationToken)
    {
        var (items, total) = await _repository.GetPagedAsync(
            request.SearchTerm, request.Page, request.PageSize, cancellationToken);

        var totalPages = (int)Math.Ceiling(total / (double)request.PageSize);

        return new PaginatedResponse<CourtResponse>(
            items.ToResponseList(), total, request.Page, request.PageSize, totalPages);
    }
}
```

### CREATE `Finnova.Service/Court/Queries/GetCourtById/GetCourtByIdQuery.cs`

```csharp
using Finnova.Models.Contracts.Courts;
using MediatR;

namespace Finnova.Service.Court.Queries.GetCourtById;

public record GetCourtByIdQuery(Guid Id) : IRequest<CourtResponse>;
```

### CREATE `Finnova.Service/Court/Queries/GetCourtById/GetCourtByIdQueryHandler.cs`

```csharp
using Finnova.Models.Contracts.Courts;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;
using MediatR;

namespace Finnova.Service.Court.Queries.GetCourtById;

public class GetCourtByIdQueryHandler : IRequestHandler<GetCourtByIdQuery, CourtResponse>
{
    private readonly ICourtRepository _repository;

    public GetCourtByIdQueryHandler(ICourtRepository repository) => _repository = repository;

    public async Task<CourtResponse> Handle(GetCourtByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new CourtNotFoundException(request.Id);   // R3.9
        return entity.ToResponse();
    }
}
```

### CREATE `Finnova.Service/Court/Queries/GetCourtAuditTrail/GetCourtAuditTrailQuery.cs`

```csharp
using Finnova.Models.Contracts.Courts;
using MediatR;

namespace Finnova.Service.Court.Queries.GetCourtAuditTrail;

public record GetCourtAuditTrailQuery(Guid CourtId) : IRequest<List<CourtAuditEntryResponse>>;
```

### CREATE `Finnova.Service/Court/Queries/GetCourtAuditTrail/GetCourtAuditTrailQueryHandler.cs`

```csharp
using Finnova.Models.Contracts.Courts;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;
using MediatR;

namespace Finnova.Service.Court.Queries.GetCourtAuditTrail;

public class GetCourtAuditTrailQueryHandler
    : IRequestHandler<GetCourtAuditTrailQuery, List<CourtAuditEntryResponse>>
{
    private readonly ICourtAuditRepository _auditRepository;

    public GetCourtAuditTrailQueryHandler(ICourtAuditRepository auditRepository)
        => _auditRepository = auditRepository;

    public async Task<List<CourtAuditEntryResponse>> Handle(
        GetCourtAuditTrailQuery request, CancellationToken cancellationToken)
    {
        var entries = await _auditRepository.GetByCourtIdAsync(request.CourtId, cancellationToken);
        return entries.Select(e => e.ToResponse()).ToList();   // R5.5, R5.6
    }
}
```

### CREATE `Finnova.Service/Mappers/CourtMapper.cs`

```csharp
using Finnova.Models.Contracts.Courts;
using Finnova.Models.Domain.Entities;

namespace Finnova.Service.Mappers;

public static class CourtMapper
{
    public static CourtResponse ToResponse(this Court x) => new(
        x.Id, x.Code, x.Name, x.CourtType, x.Jurisdiction, x.Location, x.IsActive, x.CreatedAt, x.UpdatedAt);

    public static List<CourtResponse> ToResponseList(this IEnumerable<Court> items)
        => items.Select(i => i.ToResponse()).ToList();

    public static CourtAuditEntryResponse ToResponse(this CourtAuditEntry a) => new(
        a.Id, a.CourtId, a.Action.ToString(), a.OldValues, a.NewValues, a.Summary, a.ChangedBy, a.ChangedAtUtc);
}
```

> **Note on the `Court` name collision:** the service namespace `Finnova.Service.Court` and the entity
> `Finnova.Models.Domain.Entities.Court` share the simple name `Court`. The create handler above
> references the entity by its fully-qualified name (`Finnova.Models.Domain.Entities.Court`) to avoid
> the clash. The other handlers use `GetByIdAsync` results (already typed) so they do not need the FQN.

---

## Task 7 — Backend controller + middleware branch + gateway alias

### CREATE `Finnova.SystemAdminService/Controllers/CourtController.cs`

```csharp
using System.Security.Claims;
using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.Courts;
using Finnova.Service.Court.Commands.CreateCourt;
using Finnova.Service.Court.Commands.SetCourtActive;
using Finnova.Service.Court.Commands.UpdateCourt;
using Finnova.Service.Court.Queries.GetCourtAuditTrail;
using Finnova.Service.Court.Queries.GetCourtById;
using Finnova.Service.Court.Queries.GetCourtsPaged;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finnova.SystemAdminService.Controllers;

/// <summary>
/// Court Master endpoints (FINNOVA-13). Every action is SystemAdmin-only (R7). All work flows
/// through MediatR so the ValidationBehavior pipeline runs before each handler.
/// </summary>
[ApiController]
[Route("api/court")]
public class CourtController : ControllerBase
{
    private readonly IMediator _mediator;
    public CourtController(IMediator mediator) => _mediator = mediator;

    /// <summary>Paged, filtered court list (R3).</summary>
    [HttpGet]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(PaginatedResponse<CourtResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResponse<CourtResponse>>> GetPaged(
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _mediator.Send(new GetCourtsPagedQuery(search, page, pageSize)));

    /// <summary>Single court read (R3.9). 404 when missing.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(CourtResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CourtResponse>> GetById(Guid id)
        => Ok(await _mediator.Send(new GetCourtByIdQuery(id)));

    /// <summary>Create a court (R1, R2). 409 on duplicate code.</summary>
    [HttpPost]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(CourtResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CourtResponse>> Create([FromBody] CreateCourtRequest r)
    {
        var result = await _mediator.Send(new CreateCourtCommand(
            r.Code, r.Name, r.CourtType, r.Jurisdiction, r.Location, r.IsActive, GetActingAdmin()));
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Update a court's editable fields (R4). 404 when missing.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(CourtResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CourtResponse>> Update(Guid id, [FromBody] UpdateCourtRequest r)
        => Ok(await _mediator.Send(new UpdateCourtCommand(
            id, r.Name, r.CourtType, r.Jurisdiction, r.Location, r.IsActive, GetActingAdmin())));

    /// <summary>Reactivate a court (R6.2). 404 when missing.</summary>
    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(CourtResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CourtResponse>> Activate(Guid id)
        => Ok(await _mediator.Send(new SetCourtActiveCommand(id, true, GetActingAdmin())));

    /// <summary>Deactivate a court (soft retire) (R6.1). 404 when missing.</summary>
    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(CourtResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CourtResponse>> Deactivate(Guid id)
        => Ok(await _mediator.Send(new SetCourtActiveCommand(id, false, GetActingAdmin())));

    /// <summary>Audit trail for a court, newest-first (R5.5). Missing id yields [] (R5.6).</summary>
    [HttpGet("{id:guid}/audit")]
    [Authorize(Policy = "SystemAdmin")]
    [ProducesResponseType(typeof(List<CourtAuditEntryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CourtAuditEntryResponse>>> GetAuditTrail(Guid id)
        => Ok(await _mediator.Send(new GetCourtAuditTrailQuery(id)));

    /// <summary>
    /// Resolves the acting administrator id from the validated JWT principal so the audit actor
    /// cannot be spoofed by the request body. Prefers NameIdentifier (sub), falling back to Name.
    /// </summary>
    private string GetActingAdmin()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? User.FindFirstValue("sub")
           ?? User.FindFirstValue(ClaimTypes.Name)
           ?? User.Identity?.Name
           ?? string.Empty;
}
```

### MODIFY `Finnova.SystemAdminService/Middleware/ExceptionHandlingMiddleware.cs`

**(a)** Extend the path-scoped code family. Find the block that computes `validationCode`/`fallbackCode`
(currently handling `isDcn`/`isNationality`) and add an `isCourt` case:

```csharp
var isCourt = ctx.Request.Path.StartsWithSegments("/api/court",
    StringComparison.OrdinalIgnoreCase);
var validationCode = isCourt ? "ERR-CRT-400" : isDcn ? "ERR-DCN-400" : isNationality ? "ERR-NAT-400" : "ERR-LKP-400";
var fallbackCode = isCourt ? "ERR-CRT-500" : isDcn ? "ERR-DCN-500" : isNationality ? "ERR-NAT-500" : "ERR-LKP-500";
```

**(b)** Add the typed Court branch to the `ex switch` (next to the DCN branch), before the shared
`FluentValidation.ValidationException` case:

```csharp
// ---- new Court branch (typed, no message sniffing) ----
CourtNotFoundException => (404, "ERR-CRT-404", ex.Message),
CourtDuplicateCodeException => (409, "ERR-CRT-409", ex.Message),
```

### MODIFY `Finnova.ApiGateway/appsettings.json`

Add two alias routes under `ReverseProxy.Routes` (next to the existing `ua-dcn-alias-*` routes),
so the UI's `/api/ua/api/court/**` reaches the SystemAdmin cluster:

```json
"ua-court-alias-route": {
  "ClusterId": "systemadmin-cluster",
  "Match": {
    "Path": "/api/ua/api/court/{**catch-all}"
  },
  "Transforms": [
    { "PathRemovePrefix": "/api/ua/api" },
    { "PathPrefix": "/api" }
  ]
},
"ua-court-alias-root": {
  "ClusterId": "systemadmin-cluster",
  "Match": {
    "Path": "/api/ua/api/court"
  },
  "Transforms": [
    { "PathRemovePrefix": "/api/ua/api" },
    { "PathPrefix": "/api" }
  ]
},
```

> After tasks 1–7: run the migration command (task 4), then `dotnet build Finnova.Backend.slnx -c Debug`.

---

## Task 8 — Backend tests

### CREATE `Finnova.Tests/Infrastructure/InMemoryCourtRepository.cs`

```csharp
using System.Linq.Expressions;
using Finnova.Models.Domain.Entities;
using Finnova.Repository.Interfaces;

namespace Finnova.Tests.Infrastructure;

/// <summary>Hand-written in-memory ICourtRepository mirroring the EF repo semantics.</summary>
public sealed class InMemoryCourtRepository : ICourtRepository
{
    private readonly List<Court> _store = new();

    public InMemoryCourtRepository() { }
    public InMemoryCourtRepository(IEnumerable<Court> seed) { foreach (var c in seed) _store.Add(Clone(c)); }

    public IReadOnlyList<Court> Snapshot() => _store.Select(Clone).ToList();
    public int Count => _store.Count;

    private static Court Clone(Court x) => new()
    {
        Id = x.Id, Code = x.Code, Name = x.Name, CourtType = x.CourtType,
        Jurisdiction = x.Jurisdiction, Location = x.Location, IsActive = x.IsActive,
        CreatedAt = x.CreatedAt, UpdatedAt = x.UpdatedAt,
    };

    public Task<Court?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var f = _store.FirstOrDefault(x => x.Id == id);
        return Task.FromResult(f is null ? null : Clone(f));
    }

    public Task<List<Court>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult(_store.Select(Clone).ToList());

    public Task<List<Court>> FindAsync(Expression<Func<Court, bool>> predicate, CancellationToken ct = default)
        => Task.FromResult(_store.Where(predicate.Compile()).Select(Clone).ToList());

    public Task AddAsync(Court entity, CancellationToken ct = default) { _store.Add(Clone(entity)); return Task.CompletedTask; }

    public Task UpdateAsync(Court entity, CancellationToken ct = default)
    {
        var i = _store.FindIndex(x => x.Id == entity.Id);
        if (i >= 0) _store[i] = Clone(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Court entity, CancellationToken ct = default) { _store.RemoveAll(x => x.Id == entity.Id); return Task.CompletedTask; }

    public Task<int> CountAsync(Expression<Func<Court, bool>>? predicate = null, CancellationToken ct = default)
        => Task.FromResult(predicate is null ? _store.Count : _store.Count(predicate.Compile()));

    public Task<(List<Court> Items, int Total)> GetPagedAsync(string? searchTerm, int page, int pageSize, CancellationToken ct = default)
    {
        IEnumerable<Court> q = _store;
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var t = searchTerm.Trim();
            q = q.Where(x => x.Code.Contains(t, StringComparison.OrdinalIgnoreCase)
                || x.Name.Contains(t, StringComparison.OrdinalIgnoreCase)
                || x.Jurisdiction.Contains(t, StringComparison.OrdinalIgnoreCase));
        }
        var all = q.ToList();
        var items = all
            .OrderBy(x => x.Name, StringComparer.Ordinal).ThenBy(x => x.Code, StringComparer.Ordinal)
            .Skip((page - 1) * pageSize).Take(pageSize).Select(Clone).ToList();
        return Task.FromResult((items, all.Count));
    }

    public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default)
    {
        var c = code.Trim();
        return Task.FromResult(_store.Any(x =>
            string.Equals(x.Code, c, StringComparison.OrdinalIgnoreCase) && (excludeId == null || x.Id != excludeId)));
    }
}
```

### CREATE `Finnova.Tests/Infrastructure/InMemoryCourtAuditRepository.cs`

```csharp
using System.Linq.Expressions;
using Finnova.Models.Domain.Entities;
using Finnova.Repository.Interfaces;

namespace Finnova.Tests.Infrastructure;

public sealed class InMemoryCourtAuditRepository : ICourtAuditRepository
{
    private readonly List<CourtAuditEntry> _store = new();

    public IReadOnlyList<CourtAuditEntry> Snapshot() => _store.Select(Clone).ToList();
    public int Count => _store.Count;

    private static CourtAuditEntry Clone(CourtAuditEntry x) => new()
    {
        Id = x.Id, CourtId = x.CourtId, Action = x.Action, OldValues = x.OldValues,
        NewValues = x.NewValues, Summary = x.Summary, ChangedBy = x.ChangedBy, ChangedAtUtc = x.ChangedAtUtc,
    };

    public Task<CourtAuditEntry?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var f = _store.FirstOrDefault(x => x.Id == id);
        return Task.FromResult(f is null ? null : Clone(f));
    }

    public Task<List<CourtAuditEntry>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult(_store.Select(Clone).ToList());

    public Task<List<CourtAuditEntry>> FindAsync(Expression<Func<CourtAuditEntry, bool>> predicate, CancellationToken ct = default)
        => Task.FromResult(_store.Where(predicate.Compile()).Select(Clone).ToList());

    public Task AddAsync(CourtAuditEntry entity, CancellationToken ct = default) { _store.Add(Clone(entity)); return Task.CompletedTask; }
    public Task UpdateAsync(CourtAuditEntry entity, CancellationToken ct = default) => throw new NotSupportedException("Audit entries are immutable.");
    public Task DeleteAsync(CourtAuditEntry entity, CancellationToken ct = default) => throw new NotSupportedException("Audit entries are immutable.");

    public Task<int> CountAsync(Expression<Func<CourtAuditEntry, bool>>? predicate = null, CancellationToken ct = default)
        => Task.FromResult(predicate is null ? _store.Count : _store.Count(predicate.Compile()));

    public Task<List<CourtAuditEntry>> GetByCourtIdAsync(Guid courtId, CancellationToken ct = default)
    {
        var items = _store.Where(x => x.CourtId == courtId)
            .OrderByDescending(x => x.ChangedAtUtc).ThenByDescending(x => x.Id).Select(Clone).ToList();
        return Task.FromResult(items);
    }
}
```

### CREATE `Finnova.Tests/Unit/CourtHandlerTests.cs`

```csharp
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Service.Court.Commands.CreateCourt;
using Finnova.Service.Court.Commands.SetCourtActive;
using Finnova.Service.Court.Commands.UpdateCourt;
using Finnova.Service.Court.Queries.GetCourtById;
using Finnova.Service.Court.Queries.GetCourtAuditTrail;
using Finnova.Tests.Infrastructure;
using Xunit;

namespace Finnova.Tests.Unit;

public class CourtHandlerTests
{
    private const string Admin = "admin-1";

    private static (InMemoryCourtRepository repo, InMemoryCourtAuditRepository audit) NewStores()
        => (new InMemoryCourtRepository(), new InMemoryCourtAuditRepository());

    private static CreateCourtCommand ValidCreate(
        string code = "DELHC", string name = "Delhi High Court", CourtType type = CourtType.High,
        string jur = "Delhi", string loc = "New Delhi", bool? active = null)
        => new(code, name, type, jur, loc, active, Admin);

    [Fact]
    public async Task Create_PersistsWithDefaultActiveAndWritesCreateAudit()
    {
        var (repo, audit) = NewStores();
        var handler = new CreateCourtCommandHandler(repo, audit);

        var result = await handler.Handle(ValidCreate(), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("DELHC", result.Code);
        Assert.True(result.IsActive);
        Assert.Equal(CourtType.High, result.CourtType);
        var entry = Assert.Single(audit.Snapshot());
        Assert.Equal("Create", entry.Action.ToString());
    }

    [Fact]
    public async Task Create_DuplicateCode_Throws_AndWritesNoAudit()
    {
        var (repo, audit) = NewStores();
        var handler = new CreateCourtCommandHandler(repo, audit);
        await handler.Handle(ValidCreate(code: "DELHC"), CancellationToken.None);

        await Assert.ThrowsAsync<CourtDuplicateCodeException>(() =>
            handler.Handle(ValidCreate(code: "delhc", name: "Dup"), CancellationToken.None));
        Assert.Single(audit.Snapshot());
    }

    [Fact]
    public async Task Update_NoOp_ReturnsUnchangedAndWritesNoAudit()
    {
        var (repo, audit) = NewStores();
        var created = await new CreateCourtCommandHandler(repo, audit).Handle(ValidCreate(), CancellationToken.None);
        var countAfterCreate = audit.Count;

        var update = new UpdateCourtCommandHandler(repo, audit);
        await update.Handle(new UpdateCourtCommand(
            created.Id, created.Name, created.CourtType, created.Jurisdiction, created.Location, created.IsActive, Admin),
            CancellationToken.None);

        Assert.Equal(countAfterCreate, audit.Count);
    }

    [Fact]
    public async Task Update_Changes_PersistsAndWritesUpdateAudit()
    {
        var (repo, audit) = NewStores();
        var created = await new CreateCourtCommandHandler(repo, audit).Handle(ValidCreate(), CancellationToken.None);

        var update = new UpdateCourtCommandHandler(repo, audit);
        var result = await update.Handle(new UpdateCourtCommand(
            created.Id, "Delhi HC", CourtType.High, "Delhi NCT", "New Delhi", true, Admin), CancellationToken.None);

        Assert.Equal("Delhi HC", result.Name);
        Assert.Equal("Delhi NCT", result.Jurisdiction);
        Assert.Equal(2, audit.Count);
        Assert.Contains(audit.Snapshot(), a => a.Action.ToString() == "Update");
    }

    [Fact]
    public async Task Update_UnknownId_Throws()
    {
        var (repo, audit) = NewStores();
        var update = new UpdateCourtCommandHandler(repo, audit);
        await Assert.ThrowsAsync<CourtNotFoundException>(() => update.Handle(
            new UpdateCourtCommand(Guid.NewGuid(), "X", CourtType.Other, "J", "L", true, Admin), CancellationToken.None));
    }

    [Fact]
    public async Task Deactivate_TogglesAndAudits()
    {
        var (repo, audit) = NewStores();
        var created = await new CreateCourtCommandHandler(repo, audit).Handle(ValidCreate(), CancellationToken.None);

        var setActive = new SetCourtActiveCommandHandler(repo, audit);
        var deactivated = await setActive.Handle(new SetCourtActiveCommand(created.Id, false, Admin), CancellationToken.None);

        Assert.False(deactivated.IsActive);
        Assert.Equal(2, audit.Count);
    }

    [Fact]
    public async Task SetActive_NoOp_WritesNoAudit()
    {
        var (repo, audit) = NewStores();
        var created = await new CreateCourtCommandHandler(repo, audit).Handle(ValidCreate(), CancellationToken.None);

        var setActive = new SetCourtActiveCommandHandler(repo, audit);
        await setActive.Handle(new SetCourtActiveCommand(created.Id, true, Admin), CancellationToken.None); // already active
        Assert.Equal(1, audit.Count);
    }

    [Fact]
    public async Task GetById_UnknownId_Throws()
    {
        var (repo, _) = NewStores();
        var query = new GetCourtByIdQueryHandler(repo);
        await Assert.ThrowsAsync<CourtNotFoundException>(() =>
            query.Handle(new GetCourtByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task GetAuditTrail_UnknownId_ReturnsEmpty()
    {
        var (_, audit) = NewStores();
        var query = new GetCourtAuditTrailQueryHandler(audit);
        var result = await query.Handle(new GetCourtAuditTrailQuery(Guid.NewGuid()), CancellationToken.None);
        Assert.Empty(result);
    }
}
```

### CREATE `Finnova.Tests/Integration/CourtAuthorizationTests.cs`

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace Finnova.Tests.Integration;

/// <summary>Authorization integration tests for the Court endpoints (FINNOVA-13 R7).</summary>
public class CourtAuthorizationTests : IClassFixture<SystemAdminAppFactory>
{
    private readonly SystemAdminAppFactory _factory;
    public CourtAuthorizationTests(SystemAdminAppFactory factory) => _factory = factory;

    private HttpClient Client() => _factory.CreateClient();
    private static void SetBearer(HttpClient c, string t) =>
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", t);

    private static readonly Guid SampleId = Guid.NewGuid();

    public static IEnumerable<object[]> Endpoints()
    {
        yield return new object[] { "GET list", (Func<HttpRequestMessage>)(() =>
            new HttpRequestMessage(HttpMethod.Get, "/api/court")) };
        yield return new object[] { "GET by id", (Func<HttpRequestMessage>)(() =>
            new HttpRequestMessage(HttpMethod.Get, $"/api/court/{SampleId}")) };
        yield return new object[] { "POST create", (Func<HttpRequestMessage>)(() =>
            new HttpRequestMessage(HttpMethod.Post, "/api/court")
            { Content = JsonContent.Create(new { Code = "DELHC", Name = "Delhi High Court", CourtType = "High", Jurisdiction = "Delhi", Location = "New Delhi" }) }) };
        yield return new object[] { "PUT update", (Func<HttpRequestMessage>)(() =>
            new HttpRequestMessage(HttpMethod.Put, $"/api/court/{SampleId}")
            { Content = JsonContent.Create(new { Name = "X", CourtType = "Other", Jurisdiction = "J", Location = "L", IsActive = true }) }) };
        yield return new object[] { "POST activate", (Func<HttpRequestMessage>)(() =>
            new HttpRequestMessage(HttpMethod.Post, $"/api/court/{SampleId}/activate")) };
        yield return new object[] { "POST deactivate", (Func<HttpRequestMessage>)(() =>
            new HttpRequestMessage(HttpMethod.Post, $"/api/court/{SampleId}/deactivate")) };
        yield return new object[] { "GET audit", (Func<HttpRequestMessage>)(() =>
            new HttpRequestMessage(HttpMethod.Get, $"/api/court/{SampleId}/audit")) };
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Endpoint_WithoutToken_Returns401(string name, Func<HttpRequestMessage> request)
    { _ = name; Assert.Equal(HttpStatusCode.Unauthorized, (await Client().SendAsync(request())).StatusCode); }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Endpoint_WithExpiredToken_Returns401(string name, Func<HttpRequestMessage> request)
    { _ = name; var c = Client(); SetBearer(c, DevJwt.Expired()); Assert.Equal(HttpStatusCode.Unauthorized, (await c.SendAsync(request())).StatusCode); }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Endpoint_WithTamperedToken_Returns401(string name, Func<HttpRequestMessage> request)
    { _ = name; var c = Client(); SetBearer(c, DevJwt.Tampered()); Assert.Equal(HttpStatusCode.Unauthorized, (await c.SendAsync(request())).StatusCode); }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Endpoint_WithNonAdminToken_Returns403(string name, Func<HttpRequestMessage> request)
    { _ = name; var c = Client(); SetBearer(c, DevJwt.NoRole()); Assert.Equal(HttpStatusCode.Forbidden, (await c.SendAsync(request())).StatusCode); }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Endpoint_WithInvalidAndRolelessToken_Returns401Not403(string name, Func<HttpRequestMessage> request)
    {
        _ = name; var c = Client(); SetBearer(c, DevJwt.Tampered());
        var r = await c.SendAsync(request());
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, r.StatusCode);
    }
}
```

### CREATE `Finnova.Tests/Integration/CourtEndToEndTests.cs`

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace Finnova.Tests.Integration;

/// <summary>E2E integration tests for the Court host against the in-memory provider.</summary>
public class CourtEndToEndTests : IClassFixture<SystemAdminAppFactory>
{
    private readonly SystemAdminAppFactory _factory;
    public CourtEndToEndTests(SystemAdminAppFactory factory) => _factory = factory;

    private HttpClient AdminClient()
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", DevJwt.SystemAdmin());
        return c;
    }

    private static string Unique(string p) => p + Guid.NewGuid().ToString("N")[..6];

    private async Task<CourtDto> CreateAsync(HttpClient c, string code, string name)
    {
        var res = await c.PostAsJsonAsync("/api/court", new
        {
            Code = code, Name = name, CourtType = "High", Jurisdiction = "Delhi", Location = "New Delhi",
        });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var dto = await res.Content.ReadFromJsonAsync<CourtDto>();
        Assert.NotNull(dto);
        return dto!;
    }

    [Fact]
    public async Task FullLifecycle_CreateUpdateDeactivateReactivateAudit()
    {
        var client = AdminClient();
        var created = await CreateAsync(client, Unique("HC"), "Delhi High Court");
        Assert.True(created.IsActive);

        var upd = await client.PutAsJsonAsync($"/api/court/{created.Id}", new
        {
            Name = "Delhi HC", CourtType = "High", Jurisdiction = "Delhi NCT", Location = "New Delhi", IsActive = true,
        });
        Assert.Equal(HttpStatusCode.OK, upd.StatusCode);

        var deact = await client.PostAsync($"/api/court/{created.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.OK, deact.StatusCode);
        var react = await client.PostAsync($"/api/court/{created.Id}/activate", null);
        Assert.Equal(HttpStatusCode.OK, react.StatusCode);

        var audit = await client.GetFromJsonAsync<List<AuditDto>>($"/api/court/{created.Id}/audit");
        Assert.NotNull(audit);
        Assert.Equal(4, audit!.Count);   // Create, Update, deactivate, reactivate
        Assert.Equal("Update", audit[0].Action);
        Assert.Equal("Create", audit[^1].Action);
    }

    [Fact]
    public async Task DuplicateCode_Rejected_With409()
    {
        var client = AdminClient();
        var code = Unique("DUP");
        await CreateAsync(client, code, "First");
        var dup = await client.PostAsJsonAsync("/api/court", new
        {
            Code = code, Name = "Second", CourtType = "District", Jurisdiction = "X", Location = "Y",
        });
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
        Assert.Equal("ERR-CRT-409", (await dup.Content.ReadFromJsonAsync<ProblemDto>())!.Code);
    }

    [Fact]
    public async Task MissingRequiredField_Rejected_With400()
    {
        var client = AdminClient();
        var res = await client.PostAsJsonAsync("/api/court", new
        {
            Code = Unique("BAD"), Name = "", CourtType = "High", Jurisdiction = "J", Location = "L",
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("ERR-CRT-400", (await res.Content.ReadFromJsonAsync<ProblemDto>())!.Code);
    }

    [Fact]
    public async Task GetById_UnknownId_Returns404()
    {
        var client = AdminClient();
        var res = await client.GetAsync($"/api/court/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    private sealed record CourtDto(Guid Id, string Code, string Name, string CourtType, string Jurisdiction, string Location, bool IsActive);
    private sealed record AuditDto(Guid Id, Guid CourtId, string Action, string? OldValues, string NewValues, string Summary, string ChangedBy, DateTime ChangedAtUtc);
    private sealed record ProblemDto(int? Status, string? Title, string? Detail, string? Code);
}
```

> After all backend tasks: `dotnet test Finnova.Tests\Finnova.Tests.csproj -c Debug`.

---

## Task 9 — Frontend model + services + barrels

### CREATE `src/models/court.model.ts`

```typescript
/**
 * Court Master models (FINNOVA-13). India-only platform: one English `name`. Mirrors the backend
 * contracts under Finnova.Models.Contracts.Courts. Enums serialize as strings (the SystemAdmin host
 * uses JsonStringEnumConverter), so CourtType is a string union.
 */

export type CourtType = 'Supreme' | 'High' | 'District' | 'Tribunal' | 'Other';

export interface Court {
  id: string;
  code: string;
  name: string;
  courtType: CourtType;
  jurisdiction: string;
  location: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

/** Payload for creating a court (Add dialog). */
export interface CourtFormData {
  code: string;
  name: string;
  courtType: CourtType;
  jurisdiction: string;
  location: string;
  isActive: boolean;
}

/** Payload for updating a court (Edit dialog). Code is immutable. */
export interface CourtUpdateData {
  name: string;
  courtType: CourtType;
  jurisdiction: string;
  location: string;
  isActive: boolean;
}

/** One immutable audit row (config change). */
export interface CourtAuditEntry {
  id: string;
  courtId: string;
  action: 'Create' | 'Update';
  oldValues: string | null;
  newValues: string;
  summary: string;
  changedBy: string;
  changedAtUtc: string;
}
```

### MODIFY `src/models/index.ts`

Append:

```typescript
export * from './court.model';
```

### CREATE `src/services/interfaces/court.interface.ts`

```typescript
import type {
  Court,
  CourtFormData,
  CourtUpdateData,
  CourtAuditEntry,
  PaginatedResponse,
} from '../../models';

export interface CourtQueryParams {
  search?: string;
  page?: number;
  pageSize?: number;
}

export interface ICourtService {
  getPaged(params: CourtQueryParams): Promise<PaginatedResponse<Court>>;
  getById(id: string): Promise<Court>;
  create(data: CourtFormData): Promise<Court>;
  update(id: string, data: CourtUpdateData): Promise<Court>;
  setActive(id: string, isActive: boolean): Promise<Court>;
  getAuditTrail(id: string): Promise<CourtAuditEntry[]>;
}
```

### MODIFY `src/services/interfaces/index.ts`

Append:

```typescript
export type { ICourtService, CourtQueryParams } from './court.interface';
```

### CREATE `src/services/real/court.real.ts`

```typescript
import api from '../api';
import type {
  Court,
  CourtFormData,
  CourtUpdateData,
  CourtAuditEntry,
  PaginatedResponse,
} from '../../models';
import type { ICourtService, CourtQueryParams } from '../interfaces';

/**
 * Real API implementation - calls the SystemAdminService via the API Gateway. The gateway aliases
 * /api/ua/api/court/** to the SystemAdmin cluster, so the service-relative path is '/court'. The
 * shared `api` instance injects the Bearer token and surfaces error.response.data.message via toast.
 */
class CourtRealService implements ICourtService {
  private readonly basePath = '/court';

  async getPaged(params: CourtQueryParams): Promise<PaginatedResponse<Court>> {
    const res = await api.get<PaginatedResponse<Court>>(this.basePath, {
      params: { search: params.search, page: params.page, pageSize: params.pageSize },
    });
    return res.data;
  }

  async getById(id: string): Promise<Court> {
    const res = await api.get<Court>(`${this.basePath}/${id}`);
    return res.data;
  }

  async create(data: CourtFormData): Promise<Court> {
    const res = await api.post<Court>(this.basePath, {
      code: data.code,
      name: data.name,
      courtType: data.courtType,
      jurisdiction: data.jurisdiction,
      location: data.location,
      isActive: data.isActive,
    });
    return res.data;
  }

  async update(id: string, data: CourtUpdateData): Promise<Court> {
    const res = await api.put<Court>(`${this.basePath}/${id}`, {
      name: data.name,
      courtType: data.courtType,
      jurisdiction: data.jurisdiction,
      location: data.location,
      isActive: data.isActive,
    });
    return res.data;
  }

  async setActive(id: string, isActive: boolean): Promise<Court> {
    const action = isActive ? 'activate' : 'deactivate';
    const res = await api.post<Court>(`${this.basePath}/${id}/${action}`);
    return res.data;
  }

  async getAuditTrail(id: string): Promise<CourtAuditEntry[]> {
    const res = await api.get<CourtAuditEntry[]>(`${this.basePath}/${id}/audit`);
    return res.data;
  }
}

export const courtRealService = new CourtRealService();
```

### CREATE `src/services/mock/court.mock.ts`

```typescript
import type {
  Court,
  CourtFormData,
  CourtUpdateData,
  CourtAuditEntry,
  PaginatedResponse,
} from '../../models';
import type { ICourtService, CourtQueryParams } from '../interfaces';

export const ERR_CRT_409_CODE = 'ERR-CRT-409';
export const ERR_CRT_400_CODE = 'ERR-CRT-400';
export const ERR_CRT_404_CODE = 'ERR-CRT-404';

function apiError(status: number, code: string, message: string): Error {
  const err = new Error(message) as Error & {
    response: { status: number; data: { code: string; message: string } };
  };
  err.response = { status, data: { code, message } };
  return err;
}

const SEED_DATE = '2024-01-01T00:00:00Z';

const seedCourts = (): Court[] => [
  { id: 'crt-del', code: 'DELHC', name: 'Delhi High Court', courtType: 'High', jurisdiction: 'Delhi', location: 'New Delhi', isActive: true, createdAt: SEED_DATE, updatedAt: SEED_DATE },
  { id: 'crt-sc', code: 'SCI', name: 'Supreme Court of India', courtType: 'Supreme', jurisdiction: 'India', location: 'New Delhi', isActive: true, createdAt: SEED_DATE, updatedAt: SEED_DATE },
];

const editableSnapshot = (c: Court): string =>
  JSON.stringify({ name: c.name, courtType: c.courtType, jurisdiction: c.jurisdiction, location: c.location, isActive: c.isActive });

const delay = (ms = 250) => new Promise((r) => setTimeout(r, ms));

class CourtMockService implements ICourtService {
  private courts: Court[] = seedCourts();
  private audit: CourtAuditEntry[] = [];
  private seq = 1;

  resetToSeed(): void {
    this.courts = seedCourts();
    this.audit = [];
    this.seq = 1;
  }

  async getPaged(params: CourtQueryParams): Promise<PaginatedResponse<Court>> {
    await delay();
    const term = (params.search ?? '').trim().toLowerCase();
    const page = params.page ?? 1;
    const pageSize = params.pageSize ?? 20;
    const filtered = this.courts
      .filter((c) => !term
        || c.code.toLowerCase().includes(term)
        || c.name.toLowerCase().includes(term)
        || c.jurisdiction.toLowerCase().includes(term))
      .sort((a, b) => a.name.localeCompare(b.name) || a.code.localeCompare(b.code));
    const total = filtered.length;
    const start = (page - 1) * pageSize;
    return {
      data: filtered.slice(start, start + pageSize).map((c) => ({ ...c })),
      total, page, pageSize, totalPages: Math.max(1, Math.ceil(total / pageSize)),
    };
  }

  async getById(id: string): Promise<Court> {
    await delay(150);
    const c = this.courts.find((x) => x.id === id);
    if (!c) throw apiError(404, ERR_CRT_404_CODE, `Court '${id}' was not found.`);
    return { ...c };
  }

  async create(data: CourtFormData): Promise<Court> {
    await delay();
    const code = data.code.trim();
    if (this.courts.some((c) => c.code.toLowerCase() === code.toLowerCase()))
      throw apiError(409, ERR_CRT_409_CODE, 'Court code must be unique');

    const now = new Date().toISOString();
    const court: Court = {
      id: `crt-${(this.seq++).toString(36)}-${Math.random().toString(36).slice(2, 8)}`,
      code, name: data.name.trim(), courtType: data.courtType,
      jurisdiction: data.jurisdiction.trim(), location: data.location.trim(),
      isActive: data.isActive, createdAt: now, updatedAt: now,
    };
    this.courts.push(court);
    this.audit.unshift({
      id: `aud-${court.id}-c`, courtId: court.id, action: 'Create',
      oldValues: null, newValues: editableSnapshot(court), summary: `Created court '${court.code}'.`,
      changedBy: 'mock-admin', changedAtUtc: now,
    });
    return { ...court };
  }

  async update(id: string, data: CourtUpdateData): Promise<Court> {
    await delay();
    const c = this.courts.find((x) => x.id === id);
    if (!c) throw apiError(404, ERR_CRT_404_CODE, `Court '${id}' was not found.`);

    const unchanged =
      c.name === data.name.trim() && c.courtType === data.courtType &&
      c.jurisdiction === data.jurisdiction.trim() && c.location === data.location.trim() &&
      c.isActive === data.isActive;
    if (unchanged) return { ...c };

    const before = editableSnapshot(c);
    const now = new Date().toISOString();
    c.name = data.name.trim();
    c.courtType = data.courtType;
    c.jurisdiction = data.jurisdiction.trim();
    c.location = data.location.trim();
    c.isActive = data.isActive;
    c.updatedAt = now;

    this.audit.unshift({
      id: `aud-${id}-u-${this.audit.length}`, courtId: id, action: 'Update',
      oldValues: before, newValues: editableSnapshot(c), summary: 'Updated court configuration.',
      changedBy: 'mock-admin', changedAtUtc: now,
    });
    return { ...c };
  }

  async setActive(id: string, isActive: boolean): Promise<Court> {
    await delay();
    const c = this.courts.find((x) => x.id === id);
    if (!c) throw apiError(404, ERR_CRT_404_CODE, `Court '${id}' was not found.`);
    if (c.isActive === isActive) return { ...c };

    const before = editableSnapshot(c);
    const now = new Date().toISOString();
    const old = c.isActive;
    c.isActive = isActive;
    c.updatedAt = now;
    this.audit.unshift({
      id: `aud-${id}-a-${this.audit.length}`, courtId: id, action: 'Update',
      oldValues: before, newValues: editableSnapshot(c), summary: `IsActive ${old}->${isActive}`,
      changedBy: 'mock-admin', changedAtUtc: now,
    });
    return { ...c };
  }

  async getAuditTrail(id: string): Promise<CourtAuditEntry[]> {
    await delay(150);
    return this.audit
      .filter((a) => a.courtId === id)
      .sort((a, b) => b.changedAtUtc.localeCompare(a.changedAtUtc) || b.id.localeCompare(a.id))
      .map((a) => ({ ...a }));
  }
}

export const courtMockService = new CourtMockService();
```

### CREATE `src/services/court.service.ts`

```typescript
import type { ICourtService } from './interfaces';
import { courtMockService } from './mock/court.mock';
import { courtRealService } from './real/court.real';

const useMock = import.meta.env.VITE_USE_MOCK_API === 'true';

export const courtService: ICourtService = useMock ? courtMockService : courtRealService;
```

### MODIFY `src/services/index.ts`

Append:

```typescript
export { courtService } from './court.service';
```

---

## Task 10 — Frontend page + components + route + nav

### CREATE `src/components/court/CourtGrid.tsx`

```typescript
import { Box, Chip, IconButton, Tooltip, Typography } from '@mui/material';
import EditIcon from '@mui/icons-material/Edit';
import HistoryIcon from '@mui/icons-material/History';
import ToggleOnIcon from '@mui/icons-material/ToggleOn';
import ToggleOffIcon from '@mui/icons-material/ToggleOff';
import { DataGrid } from '@mui/x-data-grid';
import type { GridColDef, GridRenderCellParams } from '@mui/x-data-grid';
import type { Court } from '../../models';

interface CourtGridProps {
  rows: Court[];
  loading: boolean;
  onEdit: (row: Court) => void;
  onToggleActive: (row: Court) => void;
  onViewAudit: (row: Court) => void;
}

function EmptyOverlay() {
  return (
    <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'center', height: '100%', p: 4 }}>
      <Typography variant="body2" color="text.secondary">No courts found.</Typography>
    </Box>
  );
}

function formatUpdated(value?: string): string {
  if (!value) return '';
  const d = new Date(value);
  return Number.isNaN(d.getTime()) ? value : d.toLocaleString();
}

export default function CourtGrid({ rows, loading, onEdit, onToggleActive, onViewAudit }: CourtGridProps) {
  const columns: GridColDef<Court>[] = [
    { field: 'code', headerName: 'Code', flex: 1, minWidth: 110 },
    { field: 'name', headerName: 'Name', flex: 1.5, minWidth: 180 },
    { field: 'courtType', headerName: 'Type', width: 120 },
    { field: 'jurisdiction', headerName: 'Jurisdiction', flex: 1, minWidth: 130 },
    { field: 'location', headerName: 'Location', flex: 1, minWidth: 130 },
    {
      field: 'isActive',
      headerName: 'Active',
      width: 100,
      sortable: false,
      renderCell: (params: GridRenderCellParams<Court, boolean>) => (
        <Chip size="small" label={params.value ? 'Active' : 'Inactive'}
          color={params.value ? 'success' : 'default'} variant={params.value ? 'filled' : 'outlined'} />
      ),
    },
    {
      field: 'updatedAt',
      headerName: 'Updated',
      flex: 1,
      minWidth: 160,
      renderCell: (params: GridRenderCellParams<Court, string>) => (
        <Typography variant="body2" color="text.secondary">{formatUpdated(params.value)}</Typography>
      ),
    },
    {
      field: 'actions',
      headerName: 'Actions',
      width: 150,
      sortable: false,
      filterable: false,
      disableColumnMenu: true,
      renderCell: (params: GridRenderCellParams<Court>) => (
        <Box>
          <Tooltip title="Edit">
            <span><IconButton size="small" onClick={() => onEdit(params.row)} aria-label="edit"><EditIcon fontSize="small" /></IconButton></span>
          </Tooltip>
          <Tooltip title={params.row.isActive ? 'Deactivate' : 'Activate'}>
            <span><IconButton size="small" onClick={() => onToggleActive(params.row)} aria-label="toggle active">
              {params.row.isActive ? <ToggleOnIcon fontSize="small" color="success" /> : <ToggleOffIcon fontSize="small" />}
            </IconButton></span>
          </Tooltip>
          <Tooltip title="View audit trail">
            <span><IconButton size="small" onClick={() => onViewAudit(params.row)} aria-label="view audit"><HistoryIcon fontSize="small" /></IconButton></span>
          </Tooltip>
        </Box>
      ),
    },
  ];

  return (
    <DataGrid
      rows={rows}
      columns={columns}
      loading={loading}
      getRowId={(row) => row.id}
      disableRowSelectionOnClick
      autoHeight
      initialState={{
        sorting: { sortModel: [{ field: 'name', sort: 'asc' }] },
        pagination: { paginationModel: { pageSize: 20, page: 0 } },
      }}
      pageSizeOptions={[10, 20, 50]}
      slots={{ noRowsOverlay: EmptyOverlay }}
      sx={{ border: 0, minHeight: 200 }}
    />
  );
}
```

### CREATE `src/components/court/CourtAddDialog.tsx`

```typescript
import { useEffect, useState } from 'react';
import {
  Dialog, DialogTitle, DialogContent, DialogActions, Button, TextField,
  FormControl, FormControlLabel, InputLabel, MenuItem, Select, Switch, Stack, CircularProgress,
} from '@mui/material';
import type { CourtFormData, CourtType } from '../../models';
import { courtService } from '../../services';

interface CourtAddDialogProps {
  open: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

interface FieldErrors { code?: string; name?: string; jurisdiction?: string; location?: string; }

const COURT_TYPES: CourtType[] = ['Supreme', 'High', 'District', 'Tribunal', 'Other'];

const EMPTY_FORM: CourtFormData = {
  code: '', name: '', courtType: 'District', jurisdiction: '', location: '', isActive: true,
};

export default function CourtAddDialog({ open, onClose, onSuccess }: CourtAddDialogProps) {
  const [form, setForm] = useState<CourtFormData>(EMPTY_FORM);
  const [errors, setErrors] = useState<FieldErrors>({});
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (open) { setForm(EMPTY_FORM); setErrors({}); setSubmitting(false); }
  }, [open]);

  const set = <K extends keyof CourtFormData>(field: K, value: CourtFormData[K]) => {
    setForm((prev) => ({ ...prev, [field]: value }));
    setErrors((prev) => ({ ...prev, [field]: undefined }));
  };

  const validate = (): boolean => {
    const next: FieldErrors = {};
    if (form.code.trim() === '') next.code = 'Code is required.';
    else if (form.code.trim().length > 20) next.code = 'Code must be at most 20 characters.';
    if (form.name.trim() === '') next.name = 'Name is required.';
    else if (form.name.trim().length > 150) next.name = 'Name must be at most 150 characters.';
    if (form.jurisdiction.trim() === '') next.jurisdiction = 'Jurisdiction is required.';
    else if (form.jurisdiction.trim().length > 100) next.jurisdiction = 'Jurisdiction must be at most 100 characters.';
    if (form.location.trim() === '') next.location = 'Location is required.';
    else if (form.location.trim().length > 100) next.location = 'Location must be at most 100 characters.';
    setErrors(next);
    return Object.keys(next).length === 0;
  };

  const handleSubmit = async () => {
    if (!validate()) return;
    setSubmitting(true);
    try {
      await courtService.create({
        ...form,
        code: form.code.trim(), name: form.name.trim(),
        jurisdiction: form.jurisdiction.trim(), location: form.location.trim(),
      });
      onSuccess();
      onClose();
    } catch {
      // Error toast handled by the shared axios interceptor / mock error; keep dialog open.
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Add Court</DialogTitle>
      <DialogContent>
        <Stack spacing={2.5} sx={{ mt: 1 }}>
          <TextField label="Code" value={form.code} onChange={(e) => set('code', e.target.value)}
            fullWidth size="small" required error={!!errors.code} helperText={errors.code}
            placeholder="e.g. DELHC" inputProps={{ maxLength: 20 }} />
          <TextField label="Name" value={form.name} onChange={(e) => set('name', e.target.value)}
            fullWidth size="small" required error={!!errors.name} helperText={errors.name}
            placeholder="e.g. Delhi High Court" inputProps={{ maxLength: 150 }} />
          <FormControl fullWidth size="small">
            <InputLabel id="add-court-type-label">Court Type</InputLabel>
            <Select labelId="add-court-type-label" label="Court Type" value={form.courtType}
              onChange={(e) => set('courtType', e.target.value as CourtType)}>
              {COURT_TYPES.map((t) => <MenuItem key={t} value={t}>{t}</MenuItem>)}
            </Select>
          </FormControl>
          <TextField label="Jurisdiction" value={form.jurisdiction} onChange={(e) => set('jurisdiction', e.target.value)}
            fullWidth size="small" required error={!!errors.jurisdiction} helperText={errors.jurisdiction}
            placeholder="e.g. Delhi" inputProps={{ maxLength: 100 }} />
          <TextField label="Location" value={form.location} onChange={(e) => set('location', e.target.value)}
            fullWidth size="small" required error={!!errors.location} helperText={errors.location}
            placeholder="e.g. New Delhi" inputProps={{ maxLength: 100 }} />
          <FormControlLabel
            control={<Switch checked={form.isActive} onChange={(e) => set('isActive', e.target.checked)} />}
            label="Active" />
        </Stack>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2 }}>
        <Button onClick={onClose} disabled={submitting}>Cancel</Button>
        <Button variant="contained" onClick={handleSubmit} disabled={submitting}
          startIcon={submitting ? <CircularProgress size={16} /> : undefined}>
          {submitting ? 'Saving...' : 'Save'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
```

### CREATE `src/components/court/CourtEditDialog.tsx`

```typescript
import { useEffect, useState } from 'react';
import {
  Dialog, DialogTitle, DialogContent, DialogActions, Button, TextField,
  FormControl, FormControlLabel, InputLabel, MenuItem, Select, Switch, Stack, CircularProgress,
} from '@mui/material';
import type { Court, CourtUpdateData, CourtType } from '../../models';
import { courtService } from '../../services';

interface CourtEditDialogProps {
  open: boolean;
  onClose: () => void;
  onSuccess: () => void;
  court: Court | null;
}

interface FieldErrors { name?: string; jurisdiction?: string; location?: string; }

const COURT_TYPES: CourtType[] = ['Supreme', 'High', 'District', 'Tribunal', 'Other'];

export default function CourtEditDialog({ open, onClose, onSuccess, court }: CourtEditDialogProps) {
  const [form, setForm] = useState<CourtUpdateData>({
    name: '', courtType: 'District', jurisdiction: '', location: '', isActive: true,
  });
  const [errors, setErrors] = useState<FieldErrors>({});
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (open && court) {
      setForm({
        name: court.name, courtType: court.courtType, jurisdiction: court.jurisdiction,
        location: court.location, isActive: court.isActive,
      });
      setErrors({});
      setSubmitting(false);
    }
  }, [open, court]);

  const set = <K extends keyof CourtUpdateData>(field: K, value: CourtUpdateData[K]) => {
    setForm((prev) => ({ ...prev, [field]: value }));
    setErrors((prev) => ({ ...prev, [field]: undefined }));
  };

  const validate = (): boolean => {
    const next: FieldErrors = {};
    if (form.name.trim() === '') next.name = 'Name is required.';
    else if (form.name.trim().length > 150) next.name = 'Name must be at most 150 characters.';
    if (form.jurisdiction.trim() === '') next.jurisdiction = 'Jurisdiction is required.';
    else if (form.jurisdiction.trim().length > 100) next.jurisdiction = 'Jurisdiction must be at most 100 characters.';
    if (form.location.trim() === '') next.location = 'Location is required.';
    else if (form.location.trim().length > 100) next.location = 'Location must be at most 100 characters.';
    setErrors(next);
    return Object.keys(next).length === 0;
  };

  const handleSubmit = async () => {
    if (!court) return;
    if (!validate()) return;
    setSubmitting(true);
    try {
      await courtService.update(court.id, {
        ...form, name: form.name.trim(), jurisdiction: form.jurisdiction.trim(), location: form.location.trim(),
      });
      onSuccess();
      onClose();
    } catch {
      // Keep dialog open; error toast handled by the shared axios interceptor.
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Edit Court</DialogTitle>
      <DialogContent>
        <Stack spacing={2.5} sx={{ mt: 1 }}>
          {/* Code is immutable after create - shown read-only. */}
          <TextField label="Code" value={court?.code ?? ''} fullWidth size="small" disabled />
          <TextField label="Name" value={form.name} onChange={(e) => set('name', e.target.value)}
            fullWidth size="small" required error={!!errors.name} helperText={errors.name} inputProps={{ maxLength: 150 }} />
          <FormControl fullWidth size="small">
            <InputLabel id="edit-court-type-label">Court Type</InputLabel>
            <Select labelId="edit-court-type-label" label="Court Type" value={form.courtType}
              onChange={(e) => set('courtType', e.target.value as CourtType)}>
              {COURT_TYPES.map((t) => <MenuItem key={t} value={t}>{t}</MenuItem>)}
            </Select>
          </FormControl>
          <TextField label="Jurisdiction" value={form.jurisdiction} onChange={(e) => set('jurisdiction', e.target.value)}
            fullWidth size="small" required error={!!errors.jurisdiction} helperText={errors.jurisdiction} inputProps={{ maxLength: 100 }} />
          <TextField label="Location" value={form.location} onChange={(e) => set('location', e.target.value)}
            fullWidth size="small" required error={!!errors.location} helperText={errors.location} inputProps={{ maxLength: 100 }} />
          <FormControlLabel
            control={<Switch checked={form.isActive} onChange={(e) => set('isActive', e.target.checked)} />}
            label="Active" />
        </Stack>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2 }}>
        <Button onClick={onClose} disabled={submitting}>Cancel</Button>
        <Button variant="contained" onClick={handleSubmit} disabled={submitting}
          startIcon={submitting ? <CircularProgress size={16} /> : undefined}>
          {submitting ? 'Saving...' : 'Save'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
```

### CREATE `src/components/court/CourtAuditDialog.tsx`

```typescript
import { useEffect, useState } from 'react';
import {
  Dialog, DialogTitle, DialogContent, DialogActions, Button, Box, Chip, CircularProgress,
  Table, TableBody, TableCell, TableHead, TableRow, Typography,
} from '@mui/material';
import type { Court, CourtAuditEntry } from '../../models';
import { courtService } from '../../services';

interface CourtAuditDialogProps {
  open: boolean;
  onClose: () => void;
  court: Court | null;
}

function formatTimestamp(value: string): string {
  const d = new Date(value);
  return Number.isNaN(d.getTime()) ? value : d.toLocaleString();
}

export default function CourtAuditDialog({ open, onClose, court }: CourtAuditDialogProps) {
  const [entries, setEntries] = useState<CourtAuditEntry[]>([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    let cancelled = false;
    if (open && court) {
      setLoading(true);
      setEntries([]);
      courtService.getAuditTrail(court.id)
        .then((r) => { if (!cancelled) setEntries(r); })
        .catch(() => { /* interceptor toasts */ })
        .finally(() => { if (!cancelled) setLoading(false); });
    }
    return () => { cancelled = true; };
  }, [open, court]);

  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle>Audit Trail{court ? ` - ${court.code}` : ''}</DialogTitle>
      <DialogContent dividers>
        {loading ? (
          <Box sx={{ display: 'flex', justifyContent: 'center', py: 4 }}><CircularProgress size={28} /></Box>
        ) : entries.length === 0 ? (
          <Box sx={{ display: 'flex', justifyContent: 'center', py: 4 }}>
            <Typography variant="body2" color="text.secondary">No audit entries found.</Typography>
          </Box>
        ) : (
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Action</TableCell>
                <TableCell>Summary</TableCell>
                <TableCell>Changed By</TableCell>
                <TableCell>Changed At</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {entries.map((entry) => (
                <TableRow key={entry.id}>
                  <TableCell>
                    <Chip size="small" label={entry.action}
                      color={entry.action === 'Create' ? 'primary' : 'warning'} variant="outlined" />
                  </TableCell>
                  <TableCell>{entry.summary}</TableCell>
                  <TableCell>{entry.changedBy}</TableCell>
                  <TableCell>{formatTimestamp(entry.changedAtUtc)}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2 }}>
        <Button onClick={onClose}>Close</Button>
      </DialogActions>
    </Dialog>
  );
}
```

### CREATE `src/pages/CourtMaster.tsx`

```typescript
import { useCallback, useEffect, useState } from 'react';
import { Box, Button, Card, CardContent, InputAdornment, Stack, TextField, Typography } from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import GavelIcon from '@mui/icons-material/Gavel';
import SearchIcon from '@mui/icons-material/Search';
import type { Court } from '../models';
import { courtService } from '../services';
import CourtGrid from '../components/court/CourtGrid';
import CourtAddDialog from '../components/court/CourtAddDialog';
import CourtEditDialog from '../components/court/CourtEditDialog';
import CourtAuditDialog from '../components/court/CourtAuditDialog';

const DEFAULT_PAGE_SIZE = 20;
const SEARCH_DEBOUNCE_MS = 300;

export default function CourtMaster() {
  const [rows, setRows] = useState<Court[]>([]);
  const [loading, setLoading] = useState(false);
  const [searchTerm, setSearchTerm] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');

  const [addOpen, setAddOpen] = useState(false);
  const [editTarget, setEditTarget] = useState<Court | null>(null);
  const [auditTarget, setAuditTarget] = useState<Court | null>(null);

  useEffect(() => {
    const handle = setTimeout(() => setDebouncedSearch(searchTerm), SEARCH_DEBOUNCE_MS);
    return () => clearTimeout(handle);
  }, [searchTerm]);

  const fetchCourts = useCallback(async () => {
    setLoading(true);
    try {
      const result = await courtService.getPaged({
        search: debouncedSearch.trim() || undefined, page: 1, pageSize: DEFAULT_PAGE_SIZE,
      });
      setRows(result.data);
    } catch {
      // Error toast surfaced by the shared axios interceptor; leave the grid unchanged.
    } finally {
      setLoading(false);
    }
  }, [debouncedSearch]);

  useEffect(() => { fetchCourts(); }, [fetchCourts]);

  const handleToggleActive = useCallback(async (row: Court) => {
    try {
      await courtService.setActive(row.id, !row.isActive);
      fetchCourts();
    } catch {
      // Interceptor toasts; leave the grid unchanged.
    }
  }, [fetchCourts]);

  return (
    <Box>
      <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 3 }}>
        <Stack direction="row" spacing={1.5} alignItems="center">
          <GavelIcon color="primary" fontSize="large" />
          <Box>
            <Typography variant="h5" fontWeight={700}>Court Master</Typography>
            <Typography variant="body2" color="text.secondary">Manage courts and their reference data</Typography>
          </Box>
        </Stack>
        <Button variant="contained" startIcon={<AddIcon />} onClick={() => setAddOpen(true)}>Add</Button>
      </Stack>

      <TextField
        placeholder="Search by code, name or jurisdiction..."
        value={searchTerm}
        onChange={(e) => setSearchTerm(e.target.value)}
        size="small"
        fullWidth
        sx={{ mb: 2 }}
        InputProps={{ startAdornment: (<InputAdornment position="start"><SearchIcon color="action" /></InputAdornment>) }}
      />

      <Card>
        <CardContent sx={{ p: 0, '&:last-child': { pb: 0 } }}>
          <CourtGrid
            rows={rows}
            loading={loading}
            onEdit={(row) => setEditTarget(row)}
            onToggleActive={handleToggleActive}
            onViewAudit={(row) => setAuditTarget(row)}
          />
        </CardContent>
      </Card>

      <CourtAddDialog open={addOpen} onClose={() => setAddOpen(false)} onSuccess={fetchCourts} />
      <CourtEditDialog open={!!editTarget} onClose={() => setEditTarget(null)} onSuccess={fetchCourts} court={editTarget} />
      <CourtAuditDialog open={!!auditTarget} onClose={() => setAuditTarget(null)} court={auditTarget} />
    </Box>
  );
}
```

### MODIFY `src/App.tsx`

Add the import near the other page imports:

```typescript
import CourtMaster from './pages/CourtMaster';
```

Add the route inside the `ProtectedRoute`/`Layout` group (next to the other master routes):

```tsx
<Route path="/courts" element={<CourtMaster />} />
```

### MODIFY `src/components/Layout.tsx`

Add the icon import near the other `@mui/icons-material` imports:

```typescript
import GavelIcon from '@mui/icons-material/Gavel';
```

Add the nav item to the **Administration** `menuGroups` entry (next to the other masters):

```tsx
{ text: 'Court Master', icon: <GavelIcon />, path: '/courts' },
```

---

## Task 11 — Frontend Vitest tests

### CREATE `src/services/mock/court.mock.test.ts`

```typescript
import { describe, it, expect, beforeEach } from 'vitest';
import { courtMockService } from './court.mock';

describe('courtMockService', () => {
  beforeEach(() => { courtMockService.resetToSeed(); });

  it('rejects duplicate code (case-insensitive) with ERR-CRT-409', async () => {
    await expect(
      courtMockService.create({ code: 'delhc', name: 'Dup', courtType: 'High', jurisdiction: 'Delhi', location: 'New Delhi', isActive: true }),
    ).rejects.toMatchObject({ response: { status: 409, data: { code: 'ERR-CRT-409', message: 'Court code must be unique' } } });
  });

  it('creates a court that is then retrievable via getPaged', async () => {
    const created = await courtMockService.create({ code: 'BOMHC', name: 'Bombay High Court', courtType: 'High', jurisdiction: 'Maharashtra', location: 'Mumbai', isActive: true });
    expect(created.id).toBeTruthy();
    const page = await courtMockService.getPaged({ page: 1, pageSize: 100 });
    expect(page.data.some((c) => c.code === 'BOMHC')).toBe(true);
  });

  it('filters by case-insensitive substring on code/name/jurisdiction', async () => {
    const result = await courtMockService.getPaged({ search: 'india', page: 1, pageSize: 100 });
    expect(result.data.every((c) =>
      c.code.toLowerCase().includes('india') || c.name.toLowerCase().includes('india') || c.jurisdiction.toLowerCase().includes('india'))).toBe(true);
  });

  it('orders by name then code', async () => {
    const result = await courtMockService.getPaged({ page: 1, pageSize: 100 });
    const names = result.data.map((c) => c.name);
    expect(names).toEqual([...names].sort((a, b) => a.localeCompare(b)));
  });

  it('update no-op writes no audit; a real change audits', async () => {
    const created = await courtMockService.create({ code: 'MADHC', name: 'Madras High Court', courtType: 'High', jurisdiction: 'Tamil Nadu', location: 'Chennai', isActive: true });
    await courtMockService.update(created.id, { name: 'Madras High Court', courtType: 'High', jurisdiction: 'Tamil Nadu', location: 'Chennai', isActive: true });
    let trail = await courtMockService.getAuditTrail(created.id);
    expect(trail).toHaveLength(1); // only Create

    await courtMockService.update(created.id, { name: 'Madras HC', courtType: 'High', jurisdiction: 'Tamil Nadu', location: 'Chennai', isActive: true });
    trail = await courtMockService.getAuditTrail(created.id);
    expect(trail).toHaveLength(2);
    expect(trail[0].action).toBe('Update');
  });

  it('deactivate/reactivate toggles and audits; unknown id -> 404', async () => {
    const created = await courtMockService.create({ code: 'KARHC', name: 'Karnataka High Court', courtType: 'High', jurisdiction: 'Karnataka', location: 'Bengaluru', isActive: true });
    const deactivated = await courtMockService.setActive(created.id, false);
    expect(deactivated.isActive).toBe(false);
    const trail = await courtMockService.getAuditTrail(created.id);
    expect(trail).toHaveLength(2);

    await expect(courtMockService.setActive('nope', false))
      .rejects.toMatchObject({ response: { status: 404, data: { code: 'ERR-CRT-404' } } });
  });

  it('getById unknown id -> 404', async () => {
    await expect(courtMockService.getById('nope'))
      .rejects.toMatchObject({ response: { status: 404, data: { code: 'ERR-CRT-404' } } });
  });
});
```

### CREATE `src/services/real/court.real.test.ts`

```typescript
import { describe, it, expect, vi, beforeEach } from 'vitest';

const getMock = vi.fn();
const postMock = vi.fn();
const putMock = vi.fn();
vi.mock('../api', () => ({
  default: {
    get: (...a: unknown[]) => getMock(...a),
    post: (...a: unknown[]) => postMock(...a),
    put: (...a: unknown[]) => putMock(...a),
  },
}));

import { courtRealService } from './court.real';

describe('courtRealService', () => {
  beforeEach(() => { getMock.mockReset(); postMock.mockReset(); putMock.mockReset(); });

  it('getPaged calls GET /court with search/page/pageSize', async () => {
    getMock.mockResolvedValue({ data: { data: [], total: 0, page: 1, pageSize: 20, totalPages: 1 } });
    await courtRealService.getPaged({ search: 'del', page: 1, pageSize: 20 });
    expect(getMock).toHaveBeenCalledWith('/court', { params: { search: 'del', page: 1, pageSize: 20 } });
  });

  it('getById calls GET /court/{id}', async () => {
    getMock.mockResolvedValue({ data: {} });
    await courtRealService.getById('abc');
    expect(getMock).toHaveBeenCalledWith('/court/abc');
  });

  it('create posts to /court with the full payload', async () => {
    postMock.mockResolvedValue({ data: {} });
    await courtRealService.create({ code: 'DELHC', name: 'Delhi High Court', courtType: 'High', jurisdiction: 'Delhi', location: 'New Delhi', isActive: true });
    expect(postMock).toHaveBeenCalledWith('/court', expect.objectContaining({ code: 'DELHC', courtType: 'High', jurisdiction: 'Delhi' }));
  });

  it('update puts to /court/{id}', async () => {
    putMock.mockResolvedValue({ data: {} });
    await courtRealService.update('9', { name: 'X', courtType: 'Other', jurisdiction: 'J', location: 'L', isActive: true });
    expect(putMock).toHaveBeenCalledWith('/court/9', expect.objectContaining({ name: 'X', courtType: 'Other' }));
  });

  it('setActive posts to activate/deactivate', async () => {
    postMock.mockResolvedValue({ data: {} });
    await courtRealService.setActive('9', true);
    expect(postMock).toHaveBeenCalledWith('/court/9/activate');
    await courtRealService.setActive('9', false);
    expect(postMock).toHaveBeenCalledWith('/court/9/deactivate');
  });

  it('getAuditTrail calls GET /court/{id}/audit', async () => {
    getMock.mockResolvedValue({ data: [] });
    await courtRealService.getAuditTrail('9');
    expect(getMock).toHaveBeenCalledWith('/court/9/audit');
  });
});
```

### CREATE `src/pages/CourtMaster.test.tsx`

```typescript
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, within, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import CourtMaster from './CourtMaster';
import type { Court, PaginatedResponse } from '../models';

const getPagedMock = vi.fn();
const getByIdMock = vi.fn();
const createMock = vi.fn();
const updateMock = vi.fn();
const setActiveMock = vi.fn();
const getAuditTrailMock = vi.fn();

vi.mock('../services', () => ({
  courtService: {
    getPaged: (...a: unknown[]) => getPagedMock(...a),
    getById: (...a: unknown[]) => getByIdMock(...a),
    create: (...a: unknown[]) => createMock(...a),
    update: (...a: unknown[]) => updateMock(...a),
    setActive: (...a: unknown[]) => setActiveMock(...a),
    getAuditTrail: (...a: unknown[]) => getAuditTrailMock(...a),
  },
}));

function makeCourt(overrides: Partial<Court> = {}): Court {
  return {
    id: 'c1', code: 'DELHC', name: 'Delhi High Court', courtType: 'High',
    jurisdiction: 'Delhi', location: 'New Delhi', isActive: true,
    createdAt: '2024-01-01T00:00:00Z', updatedAt: '2024-01-01T00:00:00Z', ...overrides,
  };
}

function paged(rows: Court[]): PaginatedResponse<Court> {
  return { data: rows, total: rows.length, page: 1, pageSize: 20, totalPages: 1 };
}

function apiError(status: number, code: string, message: string) {
  return { message, response: { status, data: { code, message } } };
}

describe('CourtMaster', () => {
  beforeEach(() => {
    getPagedMock.mockReset(); createMock.mockReset(); updateMock.mockReset();
    setActiveMock.mockReset(); getAuditTrailMock.mockReset();
  });

  it('loads and renders grid rows via getPaged on mount', async () => {
    getPagedMock.mockResolvedValue(paged([makeCourt()]));
    render(<CourtMaster />);
    expect(await screen.findByText('DELHC')).toBeInTheDocument();
    expect(await screen.findByText('Delhi High Court')).toBeInTheDocument();
  });

  it('opens the Add dialog and submits a create, refreshing on success', async () => {
    getPagedMock.mockResolvedValue(paged([makeCourt()]));
    createMock.mockResolvedValue(makeCourt({ id: 'c2', code: 'BOMHC', name: 'Bombay High Court' }));

    const user = userEvent.setup();
    render(<CourtMaster />);
    expect(await screen.findByText('DELHC')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /^add$/i }));
    const dialog = await screen.findByRole('dialog', { name: /add court/i });
    await user.type(within(dialog).getByLabelText(/^code/i), 'BOMHC');
    await user.type(within(dialog).getByLabelText(/^name/i), 'Bombay High Court');
    await user.type(within(dialog).getByLabelText(/jurisdiction/i), 'Maharashtra');
    await user.type(within(dialog).getByLabelText(/location/i), 'Mumbai');
    await user.click(within(dialog).getByRole('button', { name: /save/i }));

    await waitFor(() => expect(createMock).toHaveBeenCalledTimes(1));
    const arg = createMock.mock.calls[0][0];
    expect(arg.code).toBe('BOMHC');
    expect(arg.jurisdiction).toBe('Maharashtra');
  });

  it('duplicate-code create keeps the grid unchanged; message readable from error.response.data.message', async () => {
    getPagedMock.mockResolvedValue(paged([makeCourt()]));
    const err = apiError(409, 'ERR-CRT-409', 'Court code must be unique');
    createMock.mockRejectedValue(err);

    const user = userEvent.setup();
    render(<CourtMaster />);
    expect(await screen.findByText('DELHC')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /^add$/i }));
    const dialog = await screen.findByRole('dialog', { name: /add court/i });
    await user.type(within(dialog).getByLabelText(/^code/i), 'DELHC');
    await user.type(within(dialog).getByLabelText(/^name/i), 'Dup');
    await user.type(within(dialog).getByLabelText(/jurisdiction/i), 'Delhi');
    await user.type(within(dialog).getByLabelText(/location/i), 'New Delhi');
    await user.click(within(dialog).getByRole('button', { name: /save/i }));

    await waitFor(() => expect(createMock).toHaveBeenCalledTimes(1));
    expect(err.response.data.message).toBe('Court code must be unique');
    expect(screen.getByRole('dialog', { name: /add court/i })).toBeInTheDocument();
  });

  it('opens the Edit dialog (code read-only) and submits a rename', async () => {
    getPagedMock.mockResolvedValue(paged([makeCourt()]));
    updateMock.mockResolvedValue(makeCourt({ name: 'Delhi HC' }));

    const user = userEvent.setup();
    render(<CourtMaster />);
    const codeCell = await screen.findByText('DELHC');
    const rowEl = codeCell.closest('[role="row"]') as HTMLElement;
    await user.click(within(rowEl).getByLabelText('edit'));

    const dialog = await screen.findByRole('dialog', { name: /edit court/i });
    expect(within(dialog).getByLabelText(/^code/i)).toBeDisabled();
    const nameInput = within(dialog).getByLabelText(/^name/i);
    await user.clear(nameInput);
    await user.type(nameInput, 'Delhi HC');
    await user.click(within(dialog).getByRole('button', { name: /save/i }));

    await waitFor(() => expect(updateMock).toHaveBeenCalledWith('c1', expect.objectContaining({ name: 'Delhi HC' })));
  });

  it('toggles active state via the row action', async () => {
    getPagedMock.mockResolvedValue(paged([makeCourt({ isActive: true })]));
    setActiveMock.mockResolvedValue(makeCourt({ isActive: false }));

    const user = userEvent.setup();
    render(<CourtMaster />);
    const codeCell = await screen.findByText('DELHC');
    const rowEl = codeCell.closest('[role="row"]') as HTMLElement;
    await user.click(within(rowEl).getByLabelText('toggle active'));

    await waitFor(() => expect(setActiveMock).toHaveBeenCalledWith('c1', false));
  });

  it('opens the Audit dialog and lists entries', async () => {
    getPagedMock.mockResolvedValue(paged([makeCourt()]));
    getAuditTrailMock.mockResolvedValue([
      { id: 'a1', courtId: 'c1', action: 'Create', oldValues: null, newValues: '{}', summary: 'Created court', changedBy: 'admin', changedAtUtc: '2024-01-01T00:00:00Z' },
    ]);

    const user = userEvent.setup();
    render(<CourtMaster />);
    const codeCell = await screen.findByText('DELHC');
    const rowEl = codeCell.closest('[role="row"]') as HTMLElement;
    await user.click(within(rowEl).getByLabelText('view audit'));

    await waitFor(() => expect(getAuditTrailMock).toHaveBeenCalledWith('c1'));
    const dialog = await screen.findByRole('dialog', { name: /audit trail/i });
    expect(within(dialog).getByText('Created court')).toBeInTheDocument();
  });

  it('debounced search drives getPaged with the trimmed term', async () => {
    getPagedMock.mockResolvedValue(paged([makeCourt()]));
    const user = userEvent.setup();
    render(<CourtMaster />);
    expect(await screen.findByText('DELHC')).toBeInTheDocument();

    await user.type(screen.getByPlaceholderText(/search by code/i), 'del');
    await waitFor(() => expect(getPagedMock).toHaveBeenCalledWith({ search: 'del', page: 1, pageSize: 20 }));
  });
});
```

> After frontend tasks: `npm run build` and `npm test` in `E:\Finnova\Finnova-UI\Finnova-UI`.

---

## Post-implementation checklist

- [ ] Backend builds: `dotnet build Finnova.Backend.slnx -c Debug`
- [ ] Migration created + applied: `AddCourtAndAudit`
- [ ] Backend tests green: `dotnet test Finnova.Tests\Finnova.Tests.csproj`
- [ ] Frontend builds: `npm run build`
- [ ] Frontend tests green: `npm test`
- [ ] Manual smoke: list, create, edit, deactivate/reactivate, view audit through the gateway (`/api/ua/api/court/**`)
