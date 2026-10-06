using Finnova.Models.Contracts.UserManagement;
using Finnova.Models.Domain.Enums;
using MediatR;

namespace Finnova.Service.UserManagement.Commands.CreateUserAccount;

public record CreateUserAccountCommand(
    string Name,
    string Password,
    DateTime? DateOfJoining,
    string Designation,
    string Department,
    string? MobileNumber,
    string? Email,
    UserType UserType,
    bool? IsActive,
    string ActingAdmin) : IRequest<UserAccountResponse>;
