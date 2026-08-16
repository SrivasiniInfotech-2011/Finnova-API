using MediatR;
using Finnova.Models.Contracts.Users;

namespace Finnova.Service.Users.Commands.UpdateUser;

public record UpdateUserCommand(
    Guid Id,
    string FirstName,
    string? MiddleName,
    string LastName,
    string Email,
    string? Phone,
    string Role,
    string Status
) : IRequest<UserResponse?>;
