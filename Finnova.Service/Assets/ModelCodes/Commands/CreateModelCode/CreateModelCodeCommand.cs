using Finnova.Models.Contracts.Assets;
using MediatR;

namespace Finnova.Service.Assets.ModelCodes.Commands.CreateModelCode;

public record CreateModelCodeCommand(string Code, string Description, bool? IsActive)
    : IRequest<ModelCodeResponse>;
