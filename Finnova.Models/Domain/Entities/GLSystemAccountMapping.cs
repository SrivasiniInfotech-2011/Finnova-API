using Finnova.Models.Domain.Enums;

namespace Finnova.Models.Domain.Entities;

/// <summary>
/// Maps a (CompanyId, SystemGLType) to a GL account (R7). At most one active mapping per
/// (CompanyId, SystemGLType) — enforced by a filtered unique index.
/// </summary>
public class GLSystemAccountMapping
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public SystemGLType SystemGLType { get; set; }
    public Guid GLAccountId { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
