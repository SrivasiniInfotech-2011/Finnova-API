using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.UserManagement;
using Xunit;

namespace Finnova.Tests.Integration;

/// <summary>End-to-end: create -> by-code -> list against the SystemAdmin host (FS §9 R2/R3/R13/R14).</summary>
public class UserManagementEndToEndTests : IClassFixture<SystemAdminAppFactory>
{
    private readonly SystemAdminAppFactory _factory;
    public UserManagementEndToEndTests(SystemAdminAppFactory factory) => _factory = factory;

    // The SystemAdmin host serializes enums by name (JsonStringEnumConverter in Program.cs), so
    // the test client must deserialize the same way to read UserType off UserAccountResponse.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private HttpClient Admin()
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", DevJwt.SystemAdmin());
        return c;
    }

    [Fact]
    public async Task Create_Then_ReadByCode_Then_ListContainsIt()
    {
        var c = Admin();

        var create = await c.PostAsJsonAsync("/api/user", new
        {
            Name = "Asha Rao",
            Password = "Passw0rd!",
            Designation = "Officer",
            Department = "Operations",
            UserType = "Branch",
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<UserAccountResponse>(Json);
        Assert.NotNull(created);
        Assert.False(string.IsNullOrEmpty(created!.UserCode));   // generated code (R2.1)
        Assert.True(created.IsActive);                            // default active (R3.15)

        var read = await c.GetFromJsonAsync<UserAccountResponse>($"/api/user/by-code/{created.UserCode}", Json);
        Assert.Equal(created.UserCode, read!.UserCode);

        var list = await c.GetFromJsonAsync<PaginatedResponse<UserListItemResponse>>("/api/user?pageSize=100", Json);
        Assert.Contains(list!.Data, x => x.Code == created.UserCode);
    }
}
