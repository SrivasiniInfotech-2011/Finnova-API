using MediatR;
using Finnova.Models.Contracts.Users;

namespace Finnova.Service.Users.Queries.GetAllUsers;

public record GetAllUsersQuery : IRequest<List<UserResponse>>;
