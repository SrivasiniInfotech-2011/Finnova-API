using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;

namespace Finnova.Tests.Infrastructure;

/// <summary>Small fluent builder for constructing <see cref="Nationality"/> rows in example/edge tests.</summary>
public sealed class NationalityBuilder
{
    private readonly Nationality _n = new()
    {
        Id = Guid.NewGuid(),
        Code = "IN",
        Name = "Indian",
        IsActive = true,
        CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    public NationalityBuilder WithId(Guid id) { _n.Id = id; return this; }
    public NationalityBuilder WithCode(string code) { _n.Code = code; return this; }
    public NationalityBuilder WithName(string name) { _n.Name = name; return this; }
    public NationalityBuilder Active(bool active = true) { _n.IsActive = active; return this; }
    public NationalityBuilder WithCreatedAt(DateTime createdAt) { _n.CreatedAt = createdAt; return this; }
    public NationalityBuilder WithUpdatedAt(DateTime updatedAt) { _n.UpdatedAt = updatedAt; return this; }

    public Nationality Build() => new()
    {
        Id = _n.Id,
        Code = _n.Code,
        Name = _n.Name,
        IsActive = _n.IsActive,
        CreatedAt = _n.CreatedAt,
        UpdatedAt = _n.UpdatedAt,
    };

    /// <summary>A conventional English-only sample record (India-only platform).</summary>
    public static Nationality Sample(string code = "IN", string name = "Indian") =>
        new NationalityBuilder().WithCode(code).WithName(name).Build();
}

/// <summary>Small fluent builder for constructing <see cref="NationalityAuditEntry"/> rows in example/edge tests.</summary>
public sealed class NationalityAuditEntryBuilder
{
    private readonly NationalityAuditEntry _a = new()
    {
        Id = Guid.NewGuid(),
        NationalityId = Guid.NewGuid(),
        Action = NationalityAuditAction.Create,
        OldName = null,
        NewName = "Indian",
        ChangedBy = "admin",
        ChangedAtUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    public NationalityAuditEntryBuilder WithId(Guid id) { _a.Id = id; return this; }
    public NationalityAuditEntryBuilder ForNationality(Guid nationalityId) { _a.NationalityId = nationalityId; return this; }
    public NationalityAuditEntryBuilder WithAction(NationalityAuditAction action) { _a.Action = action; return this; }
    public NationalityAuditEntryBuilder WithOldName(string? oldName) { _a.OldName = oldName; return this; }
    public NationalityAuditEntryBuilder WithNewName(string newName) { _a.NewName = newName; return this; }
    public NationalityAuditEntryBuilder WithChangedBy(string changedBy) { _a.ChangedBy = changedBy; return this; }
    public NationalityAuditEntryBuilder WithChangedAtUtc(DateTime changedAtUtc) { _a.ChangedAtUtc = changedAtUtc; return this; }

    public NationalityAuditEntry Build() => new()
    {
        Id = _a.Id,
        NationalityId = _a.NationalityId,
        Action = _a.Action,
        OldName = _a.OldName,
        NewName = _a.NewName,
        ChangedBy = _a.ChangedBy,
        ChangedAtUtc = _a.ChangedAtUtc,
    };
}
