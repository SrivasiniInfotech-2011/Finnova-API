using Finnova.Models.Domain.Entities;
using Finnova.Repository.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Finnova.Data.Seeder.DemandSample;

public sealed class SampleOwnershipStore(FinnovaDbContext db)
{
    public async Task<SampleOwnership?> FindAsync(string entityType, string logicalKey, CancellationToken ct)
    {
        var code = $"{entityType}:{logicalKey}";
        var row = await db.LookupValues.AsNoTracking().SingleOrDefaultAsync(x => x.Module == SampleDataCatalog.OwnershipModule && x.LookupType == SampleDataCatalog.OwnershipType && x.Code == code, ct);
        if (row is null) return null;
        var parts = row.Value.Split('|');
        if (parts.Length != 4 || parts[0] != SampleDataCatalog.DatasetKey || parts[1] != entityType || !Guid.TryParse(parts[2], out var id) || parts[3].Length != 64) throw new SampleDataCollisionException(entityType, logicalKey);
        return new SampleOwnership(entityType, logicalKey, id, parts[3]);
    }

    public async Task RequireAsync(string entityType, string logicalKey, Guid entityId, CancellationToken ct)
    {
        var owner = await FindAsync(entityType, logicalKey, ct);
        if (owner is null || owner.EntityId != entityId) throw new SampleDataCollisionException(entityType, logicalKey);
    }

    public async Task RegisterAsync(string entityType, string logicalKey, Guid entityId, string contentHash, CancellationToken ct)
    {
        var code = $"{entityType}:{logicalKey}";
        var value = $"{SampleDataCatalog.DatasetKey}|{entityType}|{entityId:D}|{contentHash}";
        var row = await db.LookupValues.SingleOrDefaultAsync(x => x.Module == SampleDataCatalog.OwnershipModule && x.LookupType == SampleDataCatalog.OwnershipType && x.Code == code, ct);
        if (row is null)
        {
            db.LookupValues.Add(new LookupValue { Id = SampleDataCatalog.StableId("Ownership", code), Module = SampleDataCatalog.OwnershipModule, LookupType = SampleDataCatalog.OwnershipType, Code = code, Value = value, DisplayOrder = 0, IsActive = true, IsSystemLocked = true, CreatedAt = SampleDataCatalog.SeedTimestampUtc, UpdatedAt = SampleDataCatalog.SeedTimestampUtc });
            return;
        }
        var existing = await FindAsync(entityType, logicalKey, ct);
        if (existing is null || existing.EntityId != entityId) throw new SampleDataCollisionException(entityType, logicalKey);
        if (row.Value != value) { row.Value = value; row.UpdatedAt = SampleDataCatalog.SeedTimestampUtc; }
        row.IsActive = true; row.IsSystemLocked = true;
    }

    public Task<IReadOnlyList<SampleOwnership>> ListAsync(CancellationToken ct) => db.LookupValues.AsNoTracking().Where(x => x.Module == SampleDataCatalog.OwnershipModule && x.LookupType == SampleDataCatalog.OwnershipType && x.Code != $"Manifest:{SampleDataCatalog.DatasetKey}").OrderBy(x => x.Code).Select(x => new SampleOwnership(x.Code.Substring(0, x.Code.IndexOf(':')), x.Code.Substring(x.Code.IndexOf(':') + 1), Guid.Parse(x.Value.Split('|')[2]), x.Value.Split('|')[3])).ToListAsync(ct).ContinueWith(x => (IReadOnlyList<SampleOwnership>)x.Result, ct);
}