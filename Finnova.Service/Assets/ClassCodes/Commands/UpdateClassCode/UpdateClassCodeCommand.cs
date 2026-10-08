using MediatR;
using Finnova.Models.Contracts.Assets;

namespace Finnova.Service.Assets.ClassCodes.Commands.UpdateClassCode;

public record UpdateClassCodeCommand(Guid Id, string Code, string Description, bool IsActive)
    : IRequest<ClassCodeResponse>;
