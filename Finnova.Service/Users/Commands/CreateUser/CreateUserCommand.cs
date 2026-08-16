using MediatR;
using Finnova.Models.Contracts.Users;

namespace Finnova.Service.Users.Commands.CreateUser;

public record CreateUserCommand(
    string FirstName,
    string? MiddleName,
    string LastName,
    string Email,
    string? Phone,
    string Role,
    Guid? OrganizationId
) : IRequest<UserResponse>;
