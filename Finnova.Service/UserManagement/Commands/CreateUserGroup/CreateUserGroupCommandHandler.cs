using Finnova.Models.Contracts.UserManagement;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.UserManagement.Helpers;
using Finnova.Service.UserManagement.Mappers;
using MediatR;

namespace Finnova.Service.UserManagement.Commands.CreateUserGroup;

public class CreateUserGroupCommandHandler : IRequestHandler<CreateUserGroupCommand, UserGroupResponse>
{
    private readonly IUserManagementRepository _repository;

    public CreateUserGroupCommandHandler(IUserManagementRepository repository) => _repository = repository;

    public async Task<UserGroupResponse> Handle(CreateUserGroupCommand request, CancellationToken ct)
    {
        var codes = request.MemberUserCodes.Select(c => c.Trim()).Distinct().ToList();
        var found = await _repository.GetActiveUsersByCodesAsync(codes, ct);

        // Every tagged user must exist and be active (R5.8).
        if (found.Count != codes.Count || found.Any(u => !u.IsActive))
            throw new UserInactiveMemberException();

        var code = UserCodeGenerator.Generate(
            request.Name,
            candidate => _repository.GroupCodeExistsAsync(candidate, ct).GetAwaiter().GetResult());

        var group = new UserGroup
        {
            UserGroupCode = code,
            Name = request.Name.Trim(),
            IsActive = request.IsActive ?? true,
            Members = found.Select(u => new UserGroupMember { UserAccountId = u.Id }).ToList(),
        };

        await _repository.AddGroupAsync(group, ct);

        await _repository.AddAuditAsync(new UserManagementAuditEntry
        {
            RecordId = group.Id,
            RecordKind = UserConfiguration.UserGroup,
            Action = UserManagementAuditAction.Create,
            NewValues = $"{{\"UserGroupCode\":\"{group.UserGroupCode}\",\"Members\":{found.Count}}}",
            Summary = $"Created user group '{group.UserGroupCode}' with {found.Count} member(s).",
            ChangedBy = request.ActingAdmin,
            ChangedAtUtc = DateTime.UtcNow,
        }, ct);

        var members = found.Select(u => u.ToMemberResponse()).ToList();
        return group.ToResponse(members);
    }
}
