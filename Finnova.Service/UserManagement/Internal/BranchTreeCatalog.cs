using Finnova.Models.Contracts.UserManagement;

namespace Finnova.Service.UserManagement.Internal;

/// <summary>
/// Branch Location Tree (consumed master data, R9.1-9.3). India-only, English-only sample tree:
/// Locations -> State/Region -> Branch (MAHARASHTRA -> MUMBAI -> HEAD OFFICE, FORT BRANCH). An ALL
/// node sits at the root. Replace with the real Org-Hierarchy / Location source when wired.
/// </summary>
public static class BranchTreeCatalog
{
    public static List<BranchTreeNodeResponse> Tree() => new()
    {
        new BranchTreeNodeResponse("ALL", "ALL", "Location", Array.Empty<BranchTreeNodeResponse>()),
        new BranchTreeNodeResponse("MAHARASHTRA", "MAHARASHTRA", "Region", new List<BranchTreeNodeResponse>
        {
            new("MUMBAI", "MUMBAI", "Region", new List<BranchTreeNodeResponse>
            {
                new("HO", "HEAD OFFICE", "Branch", Array.Empty<BranchTreeNodeResponse>()),
                new("FORT", "FORT BRANCH", "Branch", Array.Empty<BranchTreeNodeResponse>()),
            }),
        }),
        new BranchTreeNodeResponse("KARNATAKA", "KARNATAKA", "Region", new List<BranchTreeNodeResponse>
        {
            new("BENGALURU", "BENGALURU", "Region", new List<BranchTreeNodeResponse>
            {
                new("MGR", "MG ROAD BRANCH", "Branch", Array.Empty<BranchTreeNodeResponse>()),
            }),
        }),
    };
}
