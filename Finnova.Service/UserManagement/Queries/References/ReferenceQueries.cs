using Finnova.Models.Contracts.UserManagement;
using MediatR;

namespace Finnova.Service.UserManagement.Queries.References;

// Active users for group members + copy-profile source (R5.5/R10.1).
public record GetActiveUsersQuery(string? Search) : IRequest<List<UserGroupMemberResponse>>;

// Active LOBs sourced from the lines_of_business master (R7.1/7.3).
public record GetAccessibleLinesOfBusinessQuery() : IRequest<List<LineOfBusinessRefResponse>>;

// Active programs sourced from the programs master.
public record GetProgramsRefQuery() : IRequest<List<ProgramRefResponse>>;

// Active role centers, includes ALL (R8.1/9.5).
public record GetRoleCentersQuery() : IRequest<List<ReferenceItemResponse>>;

// Programs for a role center as access rows with flags defaulting false (R8.3/8.4).
public record GetRoleCenterProgramsQuery(string RoleCenterName) : IRequest<List<AccessRightRow>>;

// Branch Location Tree for a LOB, includes ALL (R9.1-9.3).
public record GetBranchLocationTreeQuery(string LineOfBusiness) : IRequest<List<BranchTreeNodeResponse>>;

// Lookup-backed LOVs: Designation | Department | UserType (R3.9/3.10/3.11).
public record GetUserLookupsQuery(string Type) : IRequest<List<ReferenceItemResponse>>;
