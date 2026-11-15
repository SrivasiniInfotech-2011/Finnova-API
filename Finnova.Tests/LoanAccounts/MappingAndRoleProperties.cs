using Finnova.Models.Contracts.Internal;
using Finnova.Models.Domain.Roles;
using Finnova.Service.IdentityVerification.Mapping;
using FsCheck.Xunit;
using Xunit;

namespace Finnova.Tests.LoanAccounts;

public sealed class MappingAndRoleProperties
{
    private static string? At(string?[] parts, int i) => i < parts.Length ? parts[i] : null;

    [Property(MaxTest = 100)] // Feature: application-account-creation, Property 20: Customer Master mapping profile
    public bool MappingIsDeterministicAndJoinsNonBlankParts(string?[] nameParts, string?[] addressParts)
    {
        var name = new CustomerNameParts(At(nameParts, 0), At(nameParts, 1), At(nameParts, 2), At(nameParts, 3), At(nameParts, 4), At(nameParts, 5));
        var address = new CustomerAddressParts(At(addressParts, 0), At(addressParts, 1), At(addressParts, 2), At(addressParts, 3),
            At(addressParts, 4), At(addressParts, 5), At(addressParts, 6), At(addressParts, 7), At(addressParts, 8));

        var first = CustomerMasterMappingProfile.Map(name, address);
        var second = CustomerMasterMappingProfile.Map(name, address);

        static string Expected(string separator, IEnumerable<string?> parts) =>
            string.Join(separator, parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));
        var expectedName = Expected(" ", new[] { name.First, name.Second, name.Third, name.Fourth, name.Fifth, name.Sixth });
        var expectedAddress = Expected(", ", new[] { address.House, address.Block, address.Way, address.Area, address.Landmark,
            address.City, address.State, address.PostalCode, address.Country });
        return first == second && first.Name == expectedName && first.Address == expectedAddress;
    }

    [Fact]
    public void MissingAddressMapsToAnEmptyValue()
        => Assert.Equal(string.Empty, CustomerMasterMappingProfile.Map(new CustomerNameParts("A", null, null, null, null, null), null).Address);

    [Fact]
    public void RegistryKnowsTheCustomerMasterProfileOnly()
    {
        Assert.True(MappingProfileRegistry.Contains("customer-master"));
        Assert.False(MappingProfileRegistry.Contains("unknown-profile"));
    }

    [Property(MaxTest = 100)] // Feature: application-account-creation, Property 21: Idempotent role seeding
    public bool SeedingIsIdempotentAndPreservesExistingRoles(bool[] present, string[] others, byte repeats)
    {
        var existing = AppRoleCatalog.Roles.Where((_, i) => i < present.Length && present[i]).Select(r => r.Name)
            .Concat(others.Where(o => !string.IsNullOrWhiteSpace(o) && !AppRoleCatalog.Roles.Any(r => r.Name.Equals(o, StringComparison.OrdinalIgnoreCase))).Distinct())
            .ToList();

        IReadOnlyList<string> current = existing;
        for (var i = 0; i < 1 + repeats % 3; i++) current = AppRoleSeeder.Apply(current);

        return AppRoleCatalog.Roles.All(r => current.Count(n => n.Equals(r.Name, StringComparison.OrdinalIgnoreCase)) == 1)
            && existing.All(current.Contains)
            && current.Count == existing.Count + AppRoleSeeder.MissingRoles(existing).Count;
    }
}
