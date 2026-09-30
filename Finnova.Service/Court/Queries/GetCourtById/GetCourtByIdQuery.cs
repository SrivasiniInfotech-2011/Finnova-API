using Finnova.Models.Contracts.Courts;
using MediatR;

namespace Finnova.Service.Court.Queries.GetCourtById;

public record GetCourtByIdQuery(Guid Id) : IRequest<CourtResponse>;