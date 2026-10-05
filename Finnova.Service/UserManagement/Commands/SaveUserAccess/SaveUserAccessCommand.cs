using Finnova.Models.Contracts.UserManagement;
using MediatR;

namespace Finnova.Service.UserManagement.Commands.SaveUserAccess;

public record SaveUserAccessCommand(
    Guid Id,
    Guid LineOfBusinessId,
    IReadOnlyList<AccessRightRow> Rows,
    IReadOnlyList<BranchSelection> Branches,
    CopyProfileRequest? CopyProfile,
    string ActingAdmin) : IRequest<UserAccessResponse>;
