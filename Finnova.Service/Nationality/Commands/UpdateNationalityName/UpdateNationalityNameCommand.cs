using MediatR;
using Finnova.Models.Contracts.Nationalities;

namespace Finnova.Service.Nationality.Commands.UpdateNationalityName;

/// <summary>
/// Renames an existing nationality (only Name is editable; Code is immutable
/// post-create). ActingAdmin is resolved from the JWT by the controller and
/// recorded on the audit entry (R3, R4.1).
/// </summary>
public record UpdateNationalityNameCommand(
    Guid Id,
    string Name,
    string ActingAdmin
) : IRequest<NationalityResponse>;
