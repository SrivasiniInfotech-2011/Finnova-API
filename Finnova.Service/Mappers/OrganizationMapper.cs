using Riok.Mapperly.Abstractions;
using Finnova.Models.Contracts.Organizations;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;

namespace Finnova.Service.Mappers;

[Mapper(EnumMappingStrategy = EnumMappingStrategy.ByName)]
public static partial class OrganizationMapper
{
    [MapperIgnoreSource(nameof(Organization.Users))]
    public static partial OrganizationResponse ToResponse(this Organization organization);

    public static List<OrganizationResponse> ToResponseList(this IEnumerable<Organization> organizations)
        => organizations.Select(o => o.ToResponse()).ToList();

    private static string ConstitutionTypeToString(ConstitutionType type) => type.ToString();
    private static string OrganizationStatusToString(OrganizationStatus status) => status.ToString();
}
