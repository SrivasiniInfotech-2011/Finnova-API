using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace Finnova.Tests.Integration;

/// <summary>
/// End-to-end integration tests for the Organization Hierarchy host, exercising the full slice
/// with a valid SystemAdmin token against the in-memory host (<see cref="SystemAdminAppFactory"/>):
/// create root -> create child (level resolves to 2) -> re-parent with subtree cascade -> tree ->
/// children -> audit -> leaf delete, plus the rejection paths (duplicate code 409, self-parent 400,
/// cycle 400, has-children 409, unknown id 404).
/// </summary>
public class OrgHierarchyEndToEndTests : IClassFixture<SystemAdminAppFactory>
{
    private readonly SystemAdminAppFactory _factory;

    public OrgHierarchyEndToEndTests(SystemAdminAppFactory factory) => _factory = factory;

    private HttpClient AdminClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", DevJwt.SystemAdmin());
        return client;
    }

    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6];

    private async Task<NodeDto> CreateAsync(HttpClient client, string code, string name, Guid? parentId)
    {
        var response = await client.PostAsJsonAsync("/api/orghierarchy",
            new { Code = code, Name = name, ParentId = parentId, IsActive = true });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<NodeDto>();
        Assert.NotNull(dto);
        return dto!;
    }

    [Fact]
    public async Task FullLifecycle_CreateChildReparentTreeChildrenAuditDelete()
    {
        var client = AdminClient();

        // Create a root (level 1) and two children (level 2).
        var root = await CreateAsync(client, Unique("HO"), "Head Office", null);
        Assert.Equal(1, root.Level);
        Assert.Null(root.ParentId);

        var north = await CreateAsync(client, Unique("RGN-N"), "North Region", root.Id);
        Assert.Equal(2, north.Level);
        Assert.Equal(root.Id, north.ParentId);

        var south = await CreateAsync(client, Unique("RGN-S"), "South Region", root.Id);

        // A grandchild under North (level 3), so re-parenting North cascades a level change.
        var branch = await CreateAsync(client, Unique("BR"), "North Branch", north.Id);
        Assert.Equal(3, branch.Level);

        // Re-parent North under South: North -> level 3, its child Branch -> level 4 (cascade).
        var reparent = await client.PutAsJsonAsync($"/api/orghierarchy/{north.Id}",
            new { Name = "North Region", ParentId = south.Id });
        Assert.Equal(HttpStatusCode.OK, reparent.StatusCode);
        var movedNorth = await reparent.Content.ReadFromJsonAsync<NodeDto>();
        Assert.Equal(south.Id, movedNorth!.ParentId);
        Assert.Equal(3, movedNorth.Level);

        // The cascaded branch level is visible via the children read on North.
        var northChildren = await client.GetFromJsonAsync<List<NodeDto>>($"/api/orghierarchy/{north.Id}/children");
        Assert.NotNull(northChildren);
        var cascadedBranch = Assert.Single(northChildren!);
        Assert.Equal(4, cascadedBranch.Level);

        // Tree read returns the root with the whole subtree beneath it.
        var tree = await client.GetFromJsonAsync<List<TreeDto>>("/api/orghierarchy/tree");
        Assert.NotNull(tree);
        var treeRoot = tree!.Single(n => n.Id == root.Id);
        // Root has South and (still) has North? No: North moved under South. Root's direct children
        // are South (and any others created by parallel test methods share a fresh DB per factory).
        Assert.Contains(treeRoot.Children, c => c.Id == south.Id);

        // Audit trail for North: Create then Update (re-parent), newest first.
        var audit = await client.GetFromJsonAsync<List<AuditDto>>($"/api/orghierarchy/{north.Id}/audit");
        Assert.NotNull(audit);
        Assert.Equal(2, audit!.Count);
        Assert.Equal("Update", audit[0].Action);   // newest first
        Assert.Equal("Create", audit[1].Action);
        Assert.Equal(root.Id, audit[0].OldParentId);
        Assert.Equal(south.Id, audit[0].NewParentId);

        // Leaf delete: the branch (a leaf) is deleted -> 204, then no longer a child of North.
        var del = await client.DeleteAsync($"/api/orghierarchy/{branch.Id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);
        var afterDelete = await client.GetFromJsonAsync<List<NodeDto>>($"/api/orghierarchy/{north.Id}/children");
        Assert.Empty(afterDelete!);
    }

    [Fact]
    public async Task DuplicateCode_Rejected_With409()
    {
        var client = AdminClient();
        var code = Unique("DUP");
        await CreateAsync(client, code, "First", null);

        var dup = await client.PostAsJsonAsync("/api/orghierarchy",
            new { Code = code, Name = "Second", ParentId = (Guid?)null, IsActive = true });
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
        var problem = await dup.Content.ReadFromJsonAsync<ProblemDto>();
        Assert.Equal("ERR-ORG-409", problem!.Code);
    }

    [Fact]
    public async Task SelfParent_Rejected_With400()
    {
        var client = AdminClient();
        var node = await CreateAsync(client, Unique("SELF"), "Self", null);

        var res = await client.PutAsJsonAsync($"/api/orghierarchy/{node.Id}",
            new { Name = "Self", ParentId = node.Id });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var problem = await res.Content.ReadFromJsonAsync<ProblemDto>();
        Assert.Equal("ERR-ORG-400", problem!.Code);
    }

    [Fact]
    public async Task Cycle_Rejected_With400()
    {
        var client = AdminClient();
        var root = await CreateAsync(client, Unique("CR"), "Cycle Root", null);
        var child = await CreateAsync(client, Unique("CC"), "Cycle Child", root.Id);

        // Re-parenting root under its own descendant (child) is a cycle.
        var res = await client.PutAsJsonAsync($"/api/orghierarchy/{root.Id}",
            new { Name = "Cycle Root", ParentId = child.Id });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var problem = await res.Content.ReadFromJsonAsync<ProblemDto>();
        Assert.Equal("ERR-ORG-400", problem!.Code);
    }

    [Fact]
    public async Task DeleteWithChildren_Rejected_With409()
    {
        var client = AdminClient();
        var root = await CreateAsync(client, Unique("PR"), "Parent", null);
        await CreateAsync(client, Unique("CH"), "Child", root.Id);

        var res = await client.DeleteAsync($"/api/orghierarchy/{root.Id}");
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        var problem = await res.Content.ReadFromJsonAsync<ProblemDto>();
        Assert.Equal("ERR-ORG-409", problem!.Code);
    }

    [Fact]
    public async Task UnknownId_Delete_Returns404()
    {
        var client = AdminClient();
        var res = await client.DeleteAsync($"/api/orghierarchy/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        var problem = await res.Content.ReadFromJsonAsync<ProblemDto>();
        Assert.Equal("ERR-ORG-404", problem!.Code);
    }

    [Fact]
    public async Task CreateWithNonExistentParent_Returns400()
    {
        var client = AdminClient();
        var res = await client.PostAsJsonAsync("/api/orghierarchy",
            new { Code = Unique("NP"), Name = "Orphan", ParentId = Guid.NewGuid(), IsActive = true });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var problem = await res.Content.ReadFromJsonAsync<ProblemDto>();
        Assert.Equal("ERR-ORG-400", problem!.Code);
    }

    private sealed record NodeDto(
        Guid Id, string Code, string Name, int Level, Guid? ParentId, bool IsActive,
        DateTime CreatedAt, DateTime UpdatedAt);

    private sealed record TreeDto(
        Guid Id, string Code, string Name, int Level, Guid? ParentId, bool IsActive,
        List<TreeDto> Children);

    private sealed record AuditDto(
        Guid Id, Guid NodeId, string Action, string? OldName, string? NewName,
        Guid? OldParentId, Guid? NewParentId, string ChangedBy, DateTime ChangedAtUtc);

    private sealed record ProblemDto(int? Status, string? Title, string? Detail, string? Code);
}
