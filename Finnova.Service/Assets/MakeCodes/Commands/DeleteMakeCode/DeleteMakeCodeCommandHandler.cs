using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using MediatR;

namespace Finnova.Service.Assets.MakeCodes.Commands.DeleteMakeCode;

public class DeleteMakeCodeCommandHandler(IMakeCodeRepository repository, IAssetRepository assets) : IRequestHandler<DeleteMakeCodeCommand>
{
    private readonly IMakeCodeRepository _repository = repository;
    private readonly IAssetRepository _assets = assets;

    public async Task Handle(DeleteMakeCodeCommand request, CancellationToken ct)
    {
        var entity = await _repository.GetByIdAsync(request.Id, ct)
            ?? throw new CodeNotFoundException("Make Code", request.Id);    // R6.3 -> 404

        if (request.HardDelete)
        {
            if (await _assets.IsMakeCodeReferencedAsync(request.Id, ct))    // R6.4
                throw new CodeInUseException("Make Code");                  // -> 409
            await _repository.DeleteAsync(entity, ct);
            return;
        }

        entity.IsActive = false;                                             // R6.1 soft retire
        entity.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(entity, ct);
    }
}
