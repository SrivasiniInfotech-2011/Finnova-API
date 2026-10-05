using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

/// <summary>
/// Immutable audit record capturing one create/update/delete on a drawee bank (or a challan rule
/// under it) (R7). Written within the same unit of work as the change. Never updated or deleted
/// (R7.6). No FK to DraweeBank so entries survive independently and a missing-id read returns []
/// without error (R7.5).
/// </summary>
public class DraweeBankAuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DraweeBankId { get; set; }                   // affected aggregate (R7.1/7.2)
    public DraweeBankAuditAction Action { get; set; }        // Create | Update | Delete (R7.1/7.2)

    public string? BeforeSnapshot { get; set; }              // prior state (JSON); null on Create
    public string? AfterSnapshot { get; set; }               // new state (JSON); null on Delete

    public string ChangedBy { get; set; } = string.Empty;    // acting admin id from JWT (R7.1/7.2)
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow; // UTC timestamp (R7.1/7.2)
}
