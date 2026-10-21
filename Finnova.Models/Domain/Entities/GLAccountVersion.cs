using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

/// <summary>
/// Immutable snapshot of a pending non-structural modification (R4.3/R4.4). Exactly one Pending
/// row per GL account at a time; the last authorized configuration governs JV posting until a
/// checker authorizes this version.
/// </summary>
public class GLAccountVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GLAccountId { get; set; }
    public int VersionNumber { get; set; }
    public string ProposedPayload { get; set; } = string.Empty;   // JSON of non-structural fields
    public string MakerId { get; set; } = string.Empty;
    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;
    public GLVersionState State { get; set; } = GLVersionState.Pending;
}
