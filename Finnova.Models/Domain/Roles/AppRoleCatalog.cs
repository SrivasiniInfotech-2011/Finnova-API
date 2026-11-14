namespace Finnova.Models.Domain.Roles;

public sealed record AppRoleDefinition(Guid Id, string Name, string Description);

public static class AppRoleCatalog
{
    public const string ApplicationProcessor = "ApplicationProcessor";
    public const string AccountsViewer = "AccountsViewer";
    public const string AccountsOperator = "AccountsOperator";
    public const string KycApprover = "KycApprover";

    public static readonly IReadOnlyList<AppRoleDefinition> Roles = new[]
    {
        new AppRoleDefinition(new Guid("00000000-0000-0000-0003-000000000001"), ApplicationProcessor, "Creates and processes applications; submits KYC resolution requests (maker)."),
        new AppRoleDefinition(new Guid("00000000-0000-0000-0003-000000000002"), AccountsViewer, "Read-only access to Loan Accounts."),
        new AppRoleDefinition(new Guid("00000000-0000-0000-0003-000000000003"), AccountsOperator, "Operates the Account Creation queue and dead-letter recovery."),
        new AppRoleDefinition(new Guid("00000000-0000-0000-0003-000000000004"), KycApprover, "Approves or rejects KYC resolution requests (checker)."),
    };
}

public static class AppRoleSeeder
{
    /// <summary>Catalog roles whose name is not already present (case-insensitive).</summary>
    public static IReadOnlyList<AppRoleDefinition> MissingRoles(IEnumerable<string> existingNames)
    {
        var existing = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);
        return AppRoleCatalog.Roles.Where(r => !existing.Contains(r.Name)).ToList();
    }

    /// <summary>Returns existing names unchanged plus any missing catalog roles. Running it again adds nothing.</summary>
    public static IReadOnlyList<string> Apply(IEnumerable<string> existingNames)
    {
        var current = existingNames.ToList();
        current.AddRange(MissingRoles(current).Select(r => r.Name));
        return current;
    }
}