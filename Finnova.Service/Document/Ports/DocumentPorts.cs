namespace Finnova.Service.Document.Ports;

/// <summary>
/// Validates lookup-backed fields against ACTIVE Lookup Master values (R1.8, R4.3, R9.2).
/// Adapter delegates to the existing ILookupRepository.GetActiveByModuleAndTypeAsync.
/// </summary>
public interface ILookupValidationPort
{
    Task<bool> IsActiveLookupAsync(string lookupType, string code, CancellationToken ct = default);
}

/// <summary>
/// Outbound check for whether a document is referenced by Customer Master (FINNOVA-23) (R7).
/// Owned by the Customer Master epic; a no-op dev stub reports "not in use" until it ships.
/// </summary>
public interface IDocumentUsageChecker
{
    Task<bool> IsReferencedByCustomerMasterAsync(Guid documentId, CancellationToken ct = default);
}