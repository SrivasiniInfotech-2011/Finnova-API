using Finnova.Models.Contracts.UserManagement;
using MediatR;

namespace Finnova.Service.UserManagement.Queries.GetUserAccess;

public record GetUserAccessQuery(Guid Id, string LineOfBusiness) : IRequest<UserAccessResponse>;
