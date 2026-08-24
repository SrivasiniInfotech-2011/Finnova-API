using Riok.Mapperly.Abstractions;
using Finnova.Models.Contracts.Branches;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;

namespace Finnova.Service.Mappers;

[Mapper(EnumMappingStrategy = EnumMappingStrategy.ByName)]
public static partial class BranchMapper
{
    [MapperIgnoreSource(nameof(Branch.Organization))]
    [MapperIgnoreSource(nameof(Branch.OrganizationId))]
    public static partial BranchResponse ToResponse(this Branch branch);

    public static List<BranchResponse> ToResponseList(this IEnumerable<Branch> branches)
        => branches.Select(b => b.ToResponse()).ToList();

    private static string BranchTypeToString(BranchType type) => type.ToString();
}
