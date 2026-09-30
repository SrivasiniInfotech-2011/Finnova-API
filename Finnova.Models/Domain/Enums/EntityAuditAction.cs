namespace Finnova.Models.Domain.Enums;

/// <summary>Discriminates the configuration mutation an audit entry records (FINNOVA-11 R5).</summary>
public enum EntityAuditAction
{
    Create = 0,
    Update = 1
}