using Finnova.Models.Contracts.UserManagement;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Auth;
using Finnova.Service.UserManagement.Abstractions;
using Finnova.Service.UserManagement.Helpers;
using Finnova.Service.UserManagement.Mappers;
using MediatR;

namespace Finnova.Service.UserManagement.Commands.CreateUserAccount;

public class CreateUserAccountCommandHandler : IRequestHandler<CreateUserAccountCommand, UserAccountResponse>
{
    private readonly IUserManagementRepository _repository;
    private readonly IPasswordPolicy _passwordPolicy;

    public CreateUserAccountCommandHandler(IUserManagementRepository repository, IPasswordPolicy passwordPolicy)
    {
        _repository = repository;
        _passwordPolicy = passwordPolicy;
    }

    public async Task<UserAccountResponse> Handle(CreateUserAccountCommand request, CancellationToken ct)
    {
        // GPS password policy (R3.5) — enforced here because the policy is externally configured.
        if (!_passwordPolicy.IsCompliant(request.Password))
            throw new UserValidationException("Please enter a valid Password");

        // Generate a unique UserCode from the name after it is present (R2.1/2.7).
        var code = UserCodeGenerator.Generate(
            request.Name,
            candidate => _repository.UserCodeExistsAsync(candidate, ct).GetAwaiter().GetResult());

        var user = new UserAccount
        {
            UserCode = code,
            Name = request.Name.Trim(),
            PasswordHash = PasswordHasher.Hash(request.Password),       // never store plaintext
            DateOfJoining = request.DateOfJoining ?? DateTime.UtcNow.Date, // default to system date (R3.7)
            Designation = request.Designation.Trim(),
            Department = request.Department.Trim(),
            MobileNumber = request.MobileNumber?.Trim(),
            Email = request.Email?.Trim(),
            UserType = request.UserType,
            IsActive = request.IsActive ?? true,                        // default active (R3.15)
        };

        await _repository.AddUserAsync(user, ct);

        await _repository.AddAuditAsync(new UserManagementAuditEntry
        {
            RecordId = user.Id,
            RecordKind = UserConfiguration.User,
            Action = UserManagementAuditAction.Create,
            OldValues = null,
            NewValues = $"{{\"UserCode\":\"{user.UserCode}\",\"Name\":\"{user.Name}\"}}",
            Summary = $"Created user '{user.UserCode}'.",
            ChangedBy = request.ActingAdmin,
            ChangedAtUtc = DateTime.UtcNow,
        }, ct);

        return user.ToResponse();
    }
}
