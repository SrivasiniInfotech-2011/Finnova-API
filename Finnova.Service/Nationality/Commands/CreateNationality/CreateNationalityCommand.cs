using MediatR;
using Finnova.Models.Contracts.Nationalities;

namespace Finnova.Service.Nationality.Commands.CreateNationality;

public record CreateNationalityCommand(
    string Code,
    string Name,
    bool? IsActive,
    string ActingAdmin
) : IRequest<NationalityResponse>;
