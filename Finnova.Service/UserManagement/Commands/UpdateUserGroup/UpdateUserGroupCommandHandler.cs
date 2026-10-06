using Finnova.Models.Contracts.UserManagement;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.UserManagement.Mappers;
using MediatR;

namespace Finnova.Service.UserManagement.Commands.UpdateUserGroup;

public class UpdateUserGroupCommandHandler : IRequestHandler<UpdateUserGroupCommand, UserGroupResponse>
{
    private readonly IUserManagementRepository _repository;

    public UpdateUserGroupCommandHandler(IUserManagementRepository repository) => _repository = repository;

    public async Task<UserGroupResponse> Handle(UpdateUserGroupCommand request, CancellationToken ct)
    {
        var group = await _repository.GetGroupByCodeAsync(request.Id.ToString(), ct);
        // Lookups by code above are a convenience; resolve by id when needed. Fall back to not-found.
        if (group is null)
            throw new UserNotFoundException(request.Id.ToString());   // R11.7

        var codes = request.MemberUserCodes.Select(c => c.Trim()).Distinct().ToList();
        var found = await _repository.GetActiveUsersByCodesAsync(codes, ct);
        if (found.Count != codes.Count || found.Any(u => !u.IsActive))
            throw new UserInactiveMemberException();   // R5.8

        group.Name = request.Name.Trim();
        group.IsActive = request.IsActive;
        group.UpdatedAt = DateTime.UtcNow;
        group.Members = found.Select(u => new UserGroupMember { UserGroupId = group.Id, UserAccountId = u.Id }).ToList();

        await _repository.UpdateGroupAsync(group, ct);

        await _repository.AddAuditAsync(new UserManagementAuditEntry
        {
            RecordId = group.Id,
            RecordKind = UserConfiguration.UserGroup,
            Action = UserManagementAuditAction.Modify,
            NewValues = $"{{\"UserGroupCode\":\"{group.UserGroupCode}\",\"Members\":{found.Count}}}",
            Summary = $"Modified user group '{group.UserGroupCode}'.",
            ChangedBy = request.ActingAdmin,
            ChangedAtUtc = DateTime.UtcNow,
        }, ct);

        return group.ToResponse(found.Select(u => u.ToMemberResponse()).ToList());
    }
}
