using Finnova.Models.Contracts.Assets;
using MediatR;

namespace Finnova.Service.Assets.MakeCodes.Commands.CreateMakeCode;

public record CreateMakeCodeCommand(string Code, string Description, bool? IsActive)
    : IRequest<MakeCodeResponse>;
