using Finnova.Models.Contracts.UserManagement;
using MediatR;

namespace Finnova.Service.UserManagement.Queries.GetUserAuditTrail;

public record GetUserAuditTrailQuery(Guid RecordId) : IRequest<List<UserManagementAuditEntryResponse>>;
