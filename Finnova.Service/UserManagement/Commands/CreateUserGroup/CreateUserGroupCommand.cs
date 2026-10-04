using Finnova.Models.Contracts.UserManagement;
using MediatR;

namespace Finnova.Service.UserManagement.Commands.CreateUserGroup;

public record CreateUserGroupCommand(
    string Name,
    IReadOnlyList<string> MemberUserCodes,
    bool? IsActive,
    string ActingAdmin) : IRequest<UserGroupResponse>;
