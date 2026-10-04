using Finnova.Models.Contracts.UserManagement;
using MediatR;

namespace Finnova.Service.UserManagement.Commands.SaveUserAccess;

public record SaveUserAccessCommand(
    Guid Id,
    string LineOfBusiness,
    IReadOnlyList<AccessRightRow> Rows,
    IReadOnlyList<string> BranchCodes,
    CopyProfileRequest? CopyProfile,
    string ActingAdmin) : IRequest<UserAccessResponse>;
