using MediatR;
using Finnova.Models.Contracts.Branches;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;

namespace Finnova.Service.Branches.Commands.UpdateBranch;

public class UpdateBranchCommandHandler : IRequestHandler<UpdateBranchCommand, BranchResponse?>
{
    private readonly IBranchRepository _repository;

    public UpdateBranchCommandHandler(IBranchRepository repository)
    {
        _repository = repository;
    }

    public async Task<BranchResponse?> Handle(UpdateBranchCommand request, CancellationToken cancellationToken)
    {
        var branch = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (branch is null) return null;

        branch.BranchType = Enum.Parse<BranchType>(request.BranchType, ignoreCase: true);
        branch.CorporateCode = request.CorporateCode.ToUpperInvariant();
        branch.StateCode = request.StateCode.ToUpperInvariant();
        branch.BranchCode = request.BranchCode;
        branch.BranchName = request.BranchName;
        branch.Address = request.Address;
        branch.Landmark = request.Landmark;
        branch.State = request.State;
        branch.Country = request.Country;
        branch.Pincode = request.Pincode;
        branch.Telephone = request.Telephone;
        branch.Mobile = request.Mobile;
        branch.IsActive = request.IsActive;
        branch.IsOperational = request.IsOperational;
        branch.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(branch, cancellationToken);
        return branch.ToResponse();
    }
}
