using Finnova.Repository.Interfaces;

namespace Finnova.Service.Document.Ports;

/// <summary>
/// Reads ACTIVE lookup values for the Document Master's lookup-backed fields. The Module scope
/// is "SystemAdmin" — the module under which the Lookup Master seeds DOC_CATEGORY,
/// APPLICABLE_ENTITY, CONSTITUTION, and VALIDATION_ENGINE types that this master consumes.
/// (Assumption A2 — confirm the seeded Module value.)
/// </summary>
public sealed class LookupValidationAdapter : ILookupValidationPort
{
    public const string Module = "SystemAdmin";
    private readonly ILookupRepository _lookups;
    public LookupValidationAdapter(ILookupRepository lookups) => _lookups = lookups;

    public async Task<bool> IsActiveLookupAsync(string lookupType, string code, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        var active = await _lookups.GetActiveByModuleAndTypeAsync(Module, lookupType, ct);
        return active.Any(v => string.Equals(v.Code, code, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// DEV STUB (resolved assumption: in-use detection). Reports "not in use" so never-authorized
/// documents can be deleted (R7.2) until Customer Master (FINNOVA-23) provides a real adapter.
/// Replace before production. Fail-safe (block delete) on error/timeout is enforced by the
/// delete handler, not by this stub.
/// </summary>
public sealed class StubDocumentUsageChecker : IDocumentUsageChecker
{
    public Task<bool> IsReferencedByCustomerMasterAsync(Guid documentId, CancellationToken ct = default)
        => Task.FromResult(false);
}