using Finnova.Models.Contracts.Courts;
using Finnova.Models.Domain.Enums;
using MediatR;

namespace Finnova.Service.Court.Commands.UpdateCourt;

public record UpdateCourtCommand(
    Guid Id,
    string Name,
    CourtType CourtType,
    string Jurisdiction,
    string Location,
    bool IsActive,
    string ActingAdmin) : IRequest<CourtResponse>;