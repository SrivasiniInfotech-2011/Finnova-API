# Implementation Tasks — Resolve TestApplicationFactory

Apply these tasks in order from the repository root `d:/Projects/Finnova/Finnova-API`. The snippets are complete and copy/paste-ready. Do not modify the unrelated JvPosting files already present in the working tree. Do not add a package reference, project reference, migration, gateway route, or UI change.

## Task 1 — CREATE `Finnova.Tests/Integration/TestApplicationFactory.cs`

Create this file under the test project. The helper delegates to the existing `SystemAdminAppFactory`; it must not copy or reimplement its host, EF Core, seed, or authentication configuration.

```csharp
namespace Finnova.Tests.Integration;

/// <summary>
/// Compatibility entry point for integration tests that expect a generic test application
/// factory. The existing SystemAdminAppFactory owns all host configuration.
/// </summary>
public static class TestApplicationFactory
{
    public static SystemAdminAppFactory Create() => new();
}
```

## Task 2 — MODIFY `Finnova.Tests/JVPosting/JvPostingApiTests.cs`

Replace the complete file with the following content. The only change is the `Finnova.Tests.Integration` using directive. Keep the endpoint, null request content, and 401 assertion unchanged.

```csharp
using System.Net;
using Finnova.Tests.Integration;
using Xunit;

namespace Finnova.Tests.JVPosting;

public sealed class JvPostingApiTests
{
    [Fact]
    public async Task Submission_requires_authentication()
    {
        using var factory = TestApplicationFactory.Create();
        using var client = factory.CreateClient();
        var response = await client.PostAsync("/api/jv-posting/requests", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
```

## Post-application validation

After applying both source changes, the user may run the repository's normal compile and test validation. The expected proof is that the missing `TestApplicationFactory` symbol is resolved and the existing unauthenticated JvPosting test receives 401 through the real SystemAdmin JWT Bearer and authorization pipeline. No build, test, restore, database, npm, or server command was run while authoring this document, and this spec claims no build or test result.
