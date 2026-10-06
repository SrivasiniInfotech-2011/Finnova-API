using MediatR;

namespace Finnova.Service.UserManagement.Commands.ResetPassword;

public record ResetPasswordCommand(Guid Id, string NewPassword, string ActingAdmin) : IRequest<Unit>;
