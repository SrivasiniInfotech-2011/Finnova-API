using Finnova.Models.Contracts.UserManagement;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.UserManagement.Mappers;
using MediatR;

namespace Finnova.Service.UserManagement.Commands.UpdateUserAccount;

public class UpdateUserAccountCommandHandler : IRequestHandler<UpdateUserAccountCommand, UserAccountResponse>
{
    private readonly IUserManagementRepository _repository;

    public UpdateUserAccountCommandHandler(IUserManagementRepository repository) => _repository = repository;

    public async Task<UserAccountResponse> Handle(UpdateUserAccountCommand request, CancellationToken ct)
    {
        var user = await _repository.GetUserWithAccessAsync(request.Id, ct)
            ?? throw new UserNotFoundException(request.Id.ToString());   // R11.7

        // UserCode is immutable (R11.2) — never touched here. Password unchanged (R11.8).
        user.Name = request.Name.Trim();
        if (request.DateOfJoining is not null) user.DateOfJoining = request.DateOfJoining.Value;
        user.Designation = request.Designation.Trim();
        user.Department = request.Department.Trim();
        user.MobileNumber = request.MobileNumber?.Trim();
        user.Email = request.Email?.Trim();
        user.UserType = request.UserType;
        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateUserAsync(user, ct);

        await _repository.AddAuditAsync(new UserManagementAuditEntry
        {
            RecordId = user.Id,
            RecordKind = UserConfiguration.User,
            Action = UserManagementAuditAction.Modify,
            OldValues = null,
            NewValues = $"{{\"UserCode\":\"{user.UserCode}\",\"Name\":\"{user.Name}\"}}",
            Summary = $"Modified user '{user.UserCode}'.",
            ChangedBy = request.ActingAdmin,
            ChangedAtUtc = DateTime.UtcNow,
        }, ct);

        return user.ToResponse();
    }
}
