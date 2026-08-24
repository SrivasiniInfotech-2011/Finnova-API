using MediatR;

namespace Finnova.Service.Branches.Commands.DeleteBranch;

public record DeleteBranchCommand(Guid Id) : IRequest<bool>;
