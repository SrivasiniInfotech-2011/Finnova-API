using MediatR;
using Finnova.Models.Contracts.Assets;

namespace Finnova.Service.Assets.ModelCodes.Commands.UpdateModelCode;

public record UpdateModelCodeCommand(Guid Id, string Code, string Description, bool IsActive)
    : IRequest<ModelCodeResponse>;
