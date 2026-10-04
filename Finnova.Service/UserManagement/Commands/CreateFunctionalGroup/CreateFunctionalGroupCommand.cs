using Finnova.Models.Contracts.UserManagement;
using MediatR;

namespace Finnova.Service.UserManagement.Commands.CreateFunctionalGroup;

public record CreateFunctionalGroupCommand(
    string RoleCenterName,
    bool? IsActive,
    string ActingAdmin) : IRequest<FunctionalGroupResponse>;
