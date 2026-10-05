using Finnova.Models.Contracts.UserManagement;
using MediatR;

namespace Finnova.Service.UserManagement.Queries.GetUserAccess;

public record GetUserAccessQuery(Guid Id, Guid LineOfBusinessId) : IRequest<UserAccessResponse>;
