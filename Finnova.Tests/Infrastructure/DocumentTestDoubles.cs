using System.Linq.Expressions;
using Finnova.Models.Domain.Entities;
using Finnova.Repository.Interfaces;
using Finnova.Service.Document.Ports;

namespace Finnova.Tests.Infrastructure;

public sealed class InMemoryDocumentAuditRepository : IDocumentAuditRepository
{
    private readonly List<DocumentAuditEntry> _store = new();
    public int Count => _store.Count;
    public IReadOnlyList<DocumentAuditEntry> Snapshot() => _store.ToList();

    public Task AddAsync(DocumentAuditEntry entry, CancellationToken ct = default)
    { _store.Add(entry); return Task.CompletedTask; }

    public Task<List<DocumentAuditEntry>> GetByDocumentIdAsync(Guid documentId, CancellationToken ct = default)
        => Task.FromResult(_store.Where(x => x.DocumentId == documentId)
            .OrderByDescending(x => x.ChangedAtUtc).ThenByDescending(x => x.Id).ToList());
}

/// <summary>Fake lookup port. Treats any code in the configured active set (per type) as active.</summary>
public sealed class FakeLookupValidationPort : ILookupValidationPort
{
    private readonly Dictionary<string, HashSet<string>> _active = new(StringComparer.OrdinalIgnoreCase);
    public FakeLookupValidationPort AllowAll { get; private set; }

    public void SetActive(string lookupType, params string[] codes)
        => _active[lookupType] = new HashSet<string>(codes, StringComparer.OrdinalIgnoreCase);

    private bool _allowAll;
    public FakeLookupValidationPort WithAllowAll() { _allowAll = true; return this; }

    public Task<bool> IsActiveLookupAsync(string lookupType, string code, CancellationToken ct = default)
    {
        if (_allowAll) return Task.FromResult(!string.IsNullOrWhiteSpace(code));
        return Task.FromResult(_active.TryGetValue(lookupType, out var set)
            && code is not null && set.Contains(code));
    }
}

/// <summary>Fake usage checker: reports in-use, not-in-use, throws, or delays past the 5s timeout.</summary>
public sealed class FakeDocumentUsageChecker : IDocumentUsageChecker
{
    public bool Referenced { get; set; }
    public bool Throw { get; set; }
    public TimeSpan? Delay { get; set; }

    public async Task<bool> IsReferencedByCustomerMasterAsync(Guid documentId, CancellationToken ct = default)
    {
        if (Delay is { } d) await Task.Delay(d, ct);   // used with CancelAfter(5s) to exercise R7.5
        if (Throw) throw new InvalidOperationException("usage check failed");
        return Referenced;
    }
}