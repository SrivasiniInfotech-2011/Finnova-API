using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.UserManagement;
using Finnova.Repository.Interfaces;
using Finnova.Service.UserManagement.Mappers;
using MediatR;

namespace Finnova.Service.UserManagement.Queries.GetUserRecordsPaged;

public class GetUserRecordsPagedQueryHandler
    : IRequestHandler<GetUserRecordsPagedQuery, PaginatedResponse<UserListItemResponse>>
{
    private readonly IUserManagementRepository _repository;

    public GetUserRecordsPagedQueryHandler(IUserManagementRepository repository) => _repository = repository;

    public async Task<PaginatedResponse<UserListItemResponse>> Handle(
        GetUserRecordsPagedQuery request, CancellationToken ct)
    {
        var (items, total) = await _repository.GetPagedAsync(
            request.Search, request.Kind, request.IsActive, request.Page, request.PageSize, ct);

        var totalPages = (int)Math.Ceiling(total / (double)request.PageSize);
        return new PaginatedResponse<UserListItemResponse>(
            items.ToResponseList(), total, request.Page, request.PageSize, totalPages);
    }
}
