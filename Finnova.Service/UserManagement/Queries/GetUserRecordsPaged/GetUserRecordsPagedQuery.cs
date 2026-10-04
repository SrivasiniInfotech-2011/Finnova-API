using Finnova.Models.Contracts.Common;
using Finnova.Models.Contracts.UserManagement;
using Finnova.Models.Domain.Enums;
using MediatR;

namespace Finnova.Service.UserManagement.Queries.GetUserRecordsPaged;

public record GetUserRecordsPagedQuery(
    string? Search,
    UserConfiguration? Kind,
    bool? IsActive,
    int Page = 1,
    int PageSize = 20) : IRequest<PaginatedResponse<UserListItemResponse>>;
