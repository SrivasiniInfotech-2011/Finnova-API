using Finnova.Models.Contracts.Internal;

namespace Finnova.Service.IdentityVerification.Mapping;

/// <summary>Maps Customer Master name and address parts to verification values. Deterministic; never carries PAN or Aadhaar.</summary>
public static class CustomerMasterMappingProfile
{
    public const string Name = "customer-master";

    public static MappedVerificationValues Map(CustomerNameParts name, CustomerAddressParts? address)
    {
        var mappedName = Join(" ", name.First, name.Second, name.Third, name.Fourth, name.Fifth, name.Sixth);
        var mappedAddress = address is null
            ? string.Empty
            : Join(", ", address.House, address.Block, address.Way, address.Area, address.Landmark,
                         address.City, address.State, address.PostalCode, address.Country);
        return new MappedVerificationValues(mappedName, mappedAddress);
    }

    private static string Join(string separator, params string?[] parts) =>
        string.Join(separator, parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));
}

/// <summary>Code-level registry of mapping profile names. Existing configured profiles are unaffected (R17.5).</summary>
public static class MappingProfileRegistry
{
    private static readonly HashSet<string> Names = new(StringComparer.Ordinal) { CustomerMasterMappingProfile.Name };

    public static IReadOnlyCollection<string> RegisteredNames => Names;

    public static bool Contains(string? name) => !string.IsNullOrWhiteSpace(name) && Names.Contains(name.Trim());
}