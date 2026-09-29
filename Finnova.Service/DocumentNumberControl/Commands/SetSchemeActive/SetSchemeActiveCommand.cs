using Finnova.Models.Contracts.DocumentNumberControl;
using MediatR;

namespace Finnova.Service.DocumentNumberControl.Commands.SetSchemeActive;

/// <summary>Activates or deactivates a scheme (soft retire) (R7).</summary>
public record SetSchemeActiveCommand(Guid Id, bool IsActive, string ActingAdmin)
    : IRequest<NumberingSchemeResponse>;
