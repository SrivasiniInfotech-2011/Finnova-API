using MediatR;
using Finnova.Models.Contracts.Users;

namespace Finnova.Service.Users.Queries.GetUserById;

public record GetUserByIdQuery(Guid Id) : IRequest<UserResponse?>;
