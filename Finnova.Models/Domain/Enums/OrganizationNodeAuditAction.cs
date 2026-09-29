namespace Finnova.Models.Domain.Enums;

/// <summary>Discriminates the mutation an audit entry records (R6.1, R6.2, R6.8).</summary>
public enum OrganizationNodeAuditAction
{
    Create = 0,
    Update = 1,
    Delete = 2
}