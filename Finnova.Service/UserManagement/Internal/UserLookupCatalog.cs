using Finnova.Models.Contracts.UserManagement;

namespace Finnova.Service.UserManagement.Internal;

/// <summary>
/// Lookup-backed LOVs for Designation / Department / User Type (consumed master data,
/// R3.9/3.10/3.11). India-only, English-only sample values. Replace with the real Lookup Master
/// source when wired.
/// </summary>
public static class UserLookupCatalog
{
    public static List<ReferenceItemResponse> For(string type) => (type?.Trim()) switch
    {
        "Designation" => new List<ReferenceItemResponse>
        {
            new("MGR", "Manager"), new("OFF", "Officer"), new("CLK", "Clerk"), new("EXE", "Executive"),
        },
        "Department" => new List<ReferenceItemResponse>
        {
            new("OPS", "Operations"), new("CRD", "Credit"), new("IT", "Information Technology"), new("FIN", "Finance"),
        },
        "UserType" => new List<ReferenceItemResponse>
        {
            new("Corporate", "Corporate"), new("Branch", "Branch"),   // R3.12
        },
        _ => new List<ReferenceItemResponse>(),
    };
}
