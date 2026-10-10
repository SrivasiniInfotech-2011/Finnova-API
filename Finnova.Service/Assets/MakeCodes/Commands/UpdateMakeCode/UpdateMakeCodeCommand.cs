using MediatR;
using Finnova.Models.Contracts.Assets;

namespace Finnova.Service.Assets.MakeCodes.Commands.UpdateMakeCode;

public record UpdateMakeCodeCommand(Guid Id, string Code, string Description, bool IsActive)
    : IRequest<MakeCodeResponse>;
