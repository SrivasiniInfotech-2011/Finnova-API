namespace Finnova.Models.Domain.Enums;

/// <summary>Discriminates the mutation an audit entry records (R4.1, R4.2).</summary>
public enum NationalityAuditAction
{
    Create = 0,
    Update = 1
}
