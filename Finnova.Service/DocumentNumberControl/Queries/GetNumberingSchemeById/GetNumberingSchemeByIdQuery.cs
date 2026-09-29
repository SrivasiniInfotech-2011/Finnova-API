using Finnova.Models.Contracts.DocumentNumberControl;
using MediatR;

namespace Finnova.Service.DocumentNumberControl.Queries.GetNumberingSchemeById;

/// <summary>Reads a single scheme by id, including its live sequence state (R4.9).</summary>
public record GetNumberingSchemeByIdQuery(Guid Id) : IRequest<NumberingSchemeResponse>;
