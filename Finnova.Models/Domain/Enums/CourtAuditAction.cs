namespace Finnova.Models.Domain.Enums;

/// <summary>Discriminates the configuration mutation an audit entry records (FINNOVA-13 R5).</summary>
public enum CourtAuditAction
{
    Create = 0,
    Update = 1
}