using MediatR;
using Finnova.Models.Contracts.Assets;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;

namespace Finnova.Service.Assets.ModelCodes.Commands.UpdateModelCode;

public class UpdateModelCodeCommandHandler(IModelCodeRepository repository) : IRequestHandler<UpdateModelCodeCommand, ModelCodeResponse>
{
    private readonly IModelCodeRepository _repository = repository;

    public async Task<ModelCodeResponse> Handle(UpdateModelCodeCommand request, CancellationToken ct)
    {
        var entity = await _repository.GetByIdAsync(request.Id, ct)
            ?? throw new CodeNotFoundException("Model Code", request.Id);   // R2.6 -> 404

        var code = request.Code.Trim();
        if (await _repository.ExistsByCodeAsync(code, request.Id, ct))       // R1.2 excluding self
            throw new DuplicateCodeException("Model Code");                  // -> 409

        entity.Code = code;
        entity.Description = request.Description.Trim();
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(entity, ct);                           // R2.5
        return entity.ToResponse();
    }
}
