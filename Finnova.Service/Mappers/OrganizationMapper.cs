using Finnova.Models.Contracts.OrganizationHierarchy;
using Finnova.Models.Contracts.Organizations;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Riok.Mapperly.Abstractions;

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

    public static OrganizationNodeResponse ToResponse(this OrganizationNode x) =>
    new(x.Id, x.Code, x.Name, x.Level, x.ParentId, x.IsActive, x.CreatedAt, x.UpdatedAt);

    public static List<OrganizationNodeResponse> ToResponseList(this IEnumerable<OrganizationNode> items) =>
        items.Select(i => i.ToResponse()).ToList();

    public static OrganizationNodeAuditEntryResponse ToResponse(this OrganizationNodeAuditEntry a) =>
        new(a.Id, a.NodeId, a.Action.ToString(), a.OldName, a.NewName,
            a.OldParentId, a.NewParentId, a.ChangedBy, a.ChangedAtUtc);
}
