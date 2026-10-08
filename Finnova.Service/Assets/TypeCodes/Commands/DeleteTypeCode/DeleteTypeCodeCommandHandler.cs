using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using MediatR;

namespace Finnova.Service.Assets.TypeCodes.Commands.DeleteTypeCode;

public class DeleteTypeCodeCommandHandler(ITypeCodeRepository repository, IAssetRepository assets) : IRequestHandler<DeleteTypeCodeCommand>
{
    private readonly ITypeCodeRepository _repository = repository;
    private readonly IAssetRepository _assets = assets;

    public async Task Handle(DeleteTypeCodeCommand request, CancellationToken ct)
    {
        var entity = await _repository.GetByIdAsync(request.Id, ct)
            ?? throw new CodeNotFoundException("Type Code", request.Id);    // R6.3 -> 404

        if (request.HardDelete)
        {
            if (await _assets.IsTypeCodeReferencedAsync(request.Id, ct))    // R6.4
                throw new CodeInUseException("Type Code");                  // -> 409
            await _repository.DeleteAsync(entity, ct);
            return;
        }

        entity.IsActive = false;                                             // R6.1 soft retire
        entity.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(entity, ct);
    }
}
