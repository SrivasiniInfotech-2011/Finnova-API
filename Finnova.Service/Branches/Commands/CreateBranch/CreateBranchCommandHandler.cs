using MediatR;
using Finnova.Models.Contracts.Branches;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Enums;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;

namespace Finnova.Service.Branches.Commands.CreateBranch;

public class CreateBranchCommandHandler : IRequestHandler<CreateBranchCommand, BranchResponse>
{
    private readonly IBranchRepository _repository;

    public CreateBranchCommandHandler(IBranchRepository repository)
    {
        _repository = repository;
    }

    public async Task<BranchResponse> Handle(CreateBranchCommand request, CancellationToken cancellationToken)
    {
        var branch = new Branch
        {
            BranchType = Enum.Parse<BranchType>(request.BranchType, ignoreCase: true),
            CorporateCode = request.CorporateCode.ToUpperInvariant(),
            StateCode = request.StateCode.ToUpperInvariant(),
            BranchCode = request.BranchCode,
            BranchName = request.BranchName,
            Address = request.Address,
            Landmark = request.Landmark,
            State = request.State,
            Country = request.Country,
            Pincode = request.Pincode,
            Telephone = request.Telephone,
            Mobile = request.Mobile,
            IsActive = request.IsActive,
            IsOperational = request.IsOperational,
        };

        await _repository.AddAsync(branch, cancellationToken);
        return branch.ToResponse();
    }
}
