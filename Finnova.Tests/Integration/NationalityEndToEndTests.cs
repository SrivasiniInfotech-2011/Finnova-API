using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace Finnova.Tests.Integration;

/// <summary>
/// Task 13.2 — End-to-end create/list/audit integration test for the Nationality Master host
/// (Requirements 1.1, 4.4, 5.1, 6.3).
///
/// With a valid SystemAdmin token the full slice is exercised through the gateway-facing routes:
/// <list type="bullet">
///   <item>R1.1 — create a nationality returns 201 with the persisted record (id + resolved
///     IsActive).</item>
///   <item>R5.1 — the created record is found via a case-insensitive paged search on its code.</item>
///   <item>R4.4 — its audit trail returns 200 with exactly one Create entry.</item>
///   <item>R6.3 — the authorized SystemAdmin caller executes each operation subject to the
///     validation rules; a duplicate code (case-insensitive) is rejected with 409 ERR-NAT-409.</item>
/// </list>
///
/// The host boots in-process against the EF Core InMemory provider (see
/// <see cref="SystemAdminAppFactory"/>). The service-layer uniqueness check trims the code; on
/// SQL Server it compares case-insensitively via the default collation, whereas the InMemory
/// provider evaluates string operators ordinally. This E2E therefore reuses the exact stored code
/// to exercise the 409 ERR-NAT-409 path deterministically through the host; the case-insensitive
/// nuance of the rule is covered by the service-layer property tests.
/// </summary>
public class NationalityEndToEndTests : IClassFixture<SystemAdminAppFactory>
{
    private readonly SystemAdminAppFactory _factory;

    public NationalityEndToEndTests(SystemAdminAppFactory factory) => _factory = factory;

    private HttpClient AdminClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", DevJwt.SystemAdmin());
        return client;
    }

    [Fact]
    public async Task CreateListAudit_ThenDuplicateRejected_HappyPathAndConflict()
    {
        var client = AdminClient();

        // Unique code per run so repeated executions against a shared factory instance do not
        // collide with a record left by an earlier test method.
        var code = "IN" + Guid.NewGuid().ToString("N")[..6];

        // R1.1 — create returns 201 with the persisted record.
        var createResponse = await client.PostAsJsonAsync("/api/nationality",
            new { Code = code, Name = "Indian" });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<NationalityDto>();
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created!.Id);
        Assert.Equal(code, created.Code);
        Assert.Equal("Indian", created.Name);
        // R1.2 — IsActive was omitted, so the service defaults it to true.
        Assert.True(created.IsActive);

        // R5.1 — the record is found via a paged search on its code. The search term matches the
        // stored casing: the EF Core InMemory provider evaluates string.Contains ordinally, so
        // case-insensitive matching (a SQL-collation behavior) is validated at the service layer
        // (property tests, task 9.x) rather than through the InMemory-backed host.
        var listResponse = await client.GetAsync($"/api/nationality?search={code}");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var page = await listResponse.Content.ReadFromJsonAsync<PaginatedNationalityDto>();
        Assert.NotNull(page);
        var listed = page!.Data.SingleOrDefault(x => x.Id == created.Id);
        Assert.NotNull(listed);
        Assert.Equal(code, listed!.Code);

        // R4.4 — the audit trail returns 200 with exactly one Create entry for this nationality.
        var auditResponse = await client.GetAsync($"/api/nationality/{created.Id}/audit");
        Assert.Equal(HttpStatusCode.OK, auditResponse.StatusCode);

        var audit = await auditResponse.Content.ReadFromJsonAsync<List<NationalityAuditDto>>();
        Assert.NotNull(audit);
        var entry = Assert.Single(audit!);
        Assert.Equal(created.Id, entry.NationalityId);
        Assert.Equal("Create", entry.Action);
        Assert.Null(entry.OldName);
        Assert.Equal("Indian", entry.NewName);

        // R6.3 / R2.2 — a create reusing an existing code is rejected with 409 and the
        // ERR-NAT-409 code. The service's uniqueness check trims and (on SQL Server) compares
        // case-insensitively; under the InMemory provider the comparison is ordinal, so this
        // reuses the exact stored code to exercise the 409 path end-to-end through the host. The
        // case-insensitive nuance of the uniqueness rule is covered by the service-layer property
        // tests (task 7.3).
        var dupResponse = await client.PostAsJsonAsync("/api/nationality",
            new { Code = code, Name = "Indian Duplicate" });
        Assert.Equal(HttpStatusCode.Conflict, dupResponse.StatusCode);

        var problem = await dupResponse.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.NotNull(problem);
        Assert.Equal("ERR-NAT-409", problem!.Code);
    }

    // Deserialization DTOs (case-insensitive matching handled by the default web JSON options).
    private sealed record NationalityDto(
        Guid Id, string Code, string Name, bool IsActive, DateTime CreatedAt, DateTime UpdatedAt);

    private sealed record PaginatedNationalityDto(
        List<NationalityDto> Data, int Total, int Page, int PageSize, int TotalPages);

    private sealed record NationalityAuditDto(
        Guid Id, Guid NationalityId, string Action, string? OldName, string NewName,
        string ChangedBy, DateTime ChangedAtUtc);

    // ProblemDetails carries the stable error code in the "code" extension (see
    // ExceptionHandlingMiddleware); it deserializes onto a plain property named Code.
    private sealed record ProblemDetailsDto(int? Status, string? Title, string? Detail, string? Code);
}
