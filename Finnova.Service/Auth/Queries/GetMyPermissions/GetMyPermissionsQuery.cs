using MediatR;
using Finnova.Models.Contracts.Auth;

namespace Finnova.Service.Auth.Queries.GetMyPermissions;

/// <summary>
/// Resolve the effective per-screen permissions for the signed-in user. IsAdmin short-circuits to
/// all-active-programs-all-true; otherwise the user's access rows are collapsed per screen key.
/// </summary>
public record GetMyPermissionsQuery(Guid UserId, bool IsAdmin) : IRequest<MyPermissionsResponse>;
