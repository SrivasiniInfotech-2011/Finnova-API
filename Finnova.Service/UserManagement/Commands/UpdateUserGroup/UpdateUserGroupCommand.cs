using Finnova.Models.Contracts.UserManagement;
using MediatR;

namespace Finnova.Service.UserManagement.Commands.UpdateUserGroup;

public record UpdateUserGroupCommand(
    Guid Id,
    string Name,
    IReadOnlyList<string> MemberUserCodes,
    bool IsActive,
    string ActingAdmin) : IRequest<UserGroupResponse>;
