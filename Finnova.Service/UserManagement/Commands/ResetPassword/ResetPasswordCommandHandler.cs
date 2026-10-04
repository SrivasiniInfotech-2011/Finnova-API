using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Auth;
using Finnova.Service.UserManagement.Abstractions;
using MediatR;

namespace Finnova.Service.UserManagement.Commands.ResetPassword;

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, Unit>
{
    private readonly IUserManagementRepository _repository;
    private readonly IPasswordPolicy _passwordPolicy;

    public ResetPasswordCommandHandler(IUserManagementRepository repository, IPasswordPolicy passwordPolicy)
    {
        _repository = repository;
        _passwordPolicy = passwordPolicy;
    }

    public async Task<Unit> Handle(ResetPasswordCommand request, CancellationToken ct)
    {
        var user = await _repository.GetUserWithAccessAsync(request.Id, ct)
            ?? throw new UserNotFoundException(request.Id.ToString());   // R11.7

        // Non-compliant password: reject and preserve the existing hash unchanged (R11.6).
        if (!_passwordPolicy.IsCompliant(request.NewPassword))
            throw new UserValidationException("Please enter a valid Password");

        user.PasswordHash = PasswordHasher.Hash(request.NewPassword);   // R11.5
        user.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateUserAsync(user, ct);

        await _repository.AddAuditAsync(new UserManagementAuditEntry
        {
            RecordId = user.Id,
            RecordKind = UserConfiguration.User,
            Action = UserManagementAuditAction.Modify,
            NewValues = $"{{\"UserCode\":\"{user.UserCode}\",\"PasswordReset\":true}}",
            Summary = $"Reset password for user '{user.UserCode}'.",
            ChangedBy = request.ActingAdmin,
            ChangedAtUtc = DateTime.UtcNow,
        }, ct);

        return Unit.Value;
    }
}
