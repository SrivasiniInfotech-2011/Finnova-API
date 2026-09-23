using Finnova.Models.Domain.Entities;

namespace Finnova.Tests.Infrastructure;

/// <summary>Small fluent builder for constructing LookupValue rows in example/edge tests.</summary>
public sealed class LookupValueBuilder
{
    private readonly LookupValue _v = new()
    {
        Id = Guid.NewGuid(),
        Module = "Origination",
        LookupType = "MARITAL_STATUS",
        Code = "SIN",
        Value = "Single",
        DisplayOrder = 1,
        IsActive = true,
        IsSystemLocked = false,
        CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    public LookupValueBuilder WithId(Guid id) { _v.Id = id; return this; }
    public LookupValueBuilder WithModule(string m) { _v.Module = m; return this; }
    public LookupValueBuilder WithType(string t) { _v.LookupType = t; return this; }
    public LookupValueBuilder WithCode(string c) { _v.Code = c; return this; }
    public LookupValueBuilder WithValue(string v) { _v.Value = v; return this; }
    public LookupValueBuilder WithDisplayOrder(int o) { _v.DisplayOrder = o; return this; }
    public LookupValueBuilder Active(bool a = true) { _v.IsActive = a; return this; }
    public LookupValueBuilder Locked(bool l = true) { _v.IsSystemLocked = l; return this; }

    public LookupValue Build() => new()
    {
        Id = _v.Id,
        Module = _v.Module,
        LookupType = _v.LookupType,
        Code = _v.Code,
        Value = _v.Value,
        DisplayOrder = _v.DisplayOrder,
        IsActive = _v.IsActive,
        IsSystemLocked = _v.IsSystemLocked,
        CreatedAt = _v.CreatedAt,
        UpdatedAt = _v.UpdatedAt,
    };

    public static LookupValue SystemLocked(string code = "DEBIT") =>
        new LookupValueBuilder()
            .WithModule("SystemAdmin")
            .WithType("SYS_TXN_TYPE")
            .WithCode(code)
            .WithValue("Debit")
            .WithDisplayOrder(1)
            .Locked()
            .Build();
}
