namespace Finnova.Models.Domain.Enums;

/// <summary>Discriminates the mutation an audit entry records (R7.1, R7.2).</summary>
public enum DraweeBankAuditAction
{
    Create = 0,
    Update = 1,
    Delete = 2
}
