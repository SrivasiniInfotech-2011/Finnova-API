using MediatR;
using Finnova.Models.Contracts.Assets;

namespace Finnova.Service.Assets.TypeCodes.Commands.UpdateTypeCode;

public record UpdateTypeCodeCommand(Guid Id, string Code, string Description, bool IsActive)
    : IRequest<TypeCodeResponse>;
