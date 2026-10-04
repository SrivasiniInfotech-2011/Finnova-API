using Finnova.Models.Contracts.UserManagement;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.UserManagement.Mappers;
using MediatR;

namespace Finnova.Service.UserManagement.Queries.GetUserRecordByCode;

public class GetUserRecordByCodeQueryHandler : IRequestHandler<GetUserRecordByCodeQuery, UserAccountResponse>
{
    private readonly IUserManagementRepository _repository;

    public GetUserRecordByCodeQueryHandler(IUserManagementRepository repository) => _repository = repository;

    public async Task<UserAccountResponse> Handle(GetUserRecordByCodeQuery request, CancellationToken ct)
    {
        var user = await _repository.GetUserByCodeAsync(request.Code, ct)
            ?? throw new UserNotFoundException(request.Code);   // R11.7/R12.3
        return user.ToResponse();
    }
}
