using Finnova.Models.Contracts.Courts;
using MediatR;

namespace Finnova.Service.Court.Commands.SetCourtActive;

/// <summary>Activates or deactivates a court (soft retire) (R6).</summary>
public record SetCourtActiveCommand(Guid Id, bool IsActive, string ActingAdmin) : IRequest<CourtResponse>;