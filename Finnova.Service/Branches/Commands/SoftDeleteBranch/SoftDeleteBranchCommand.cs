using MediatR;

namespace Finnova.Service.Branches.Commands.SoftDeleteBranch;

public record SoftDeleteBranchCommand(Guid Id) : IRequest<bool>;
