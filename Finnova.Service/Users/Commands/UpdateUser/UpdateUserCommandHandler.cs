using MediatR;
using Finnova.Models.Contracts.Users;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;

namespace Finnova.Service.Users.Commands.UpdateUser;

public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, UserResponse?>
{
    private readonly IUserRepository _repository;

    public UpdateUserCommandHandler(IUserRepository repository)
    {
        _repository = repository;
    }

    public async Task<UserResponse?> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (user is null) return null;

        user.FirstName = request.FirstName;
        user.MiddleName = request.MiddleName;
        user.LastName = request.LastName;
        user.Email = request.Email;
        user.Phone = request.Phone;
        user.Role = Enum.Parse<UserRole>(request.Role, ignoreCase: true);
        user.Status = Enum.Parse<UserStatus>(request.Status, ignoreCase: true);
        user.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(user, cancellationToken);
        return user.ToResponse();
    }
}
