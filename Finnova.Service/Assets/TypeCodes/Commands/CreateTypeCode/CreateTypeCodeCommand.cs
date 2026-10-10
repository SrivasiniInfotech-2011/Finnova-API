using Finnova.Models.Contracts.Assets;
using MediatR;

namespace Finnova.Service.Assets.TypeCodes.Commands.CreateTypeCode;

public record CreateTypeCodeCommand(string Code, string Description, bool? IsActive)
    : IRequest<TypeCodeResponse>;
