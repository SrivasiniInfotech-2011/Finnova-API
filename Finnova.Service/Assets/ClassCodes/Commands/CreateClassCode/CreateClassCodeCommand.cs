using MediatR;
using Finnova.Models.Contracts.Assets;

namespace Finnova.Service.Assets.ClassCodes.Commands.CreateClassCode;

public record CreateClassCodeCommand(string Code, string Description, bool? IsActive)
    : IRequest<ClassCodeResponse>;
