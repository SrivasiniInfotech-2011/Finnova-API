using MediatR;
using Finnova.Models.Contracts.Assets;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;

namespace Finnova.Service.Assets.ClassCodes.Commands.UpdateClassCode;

public class UpdateClassCodeCommandHandler : IRequestHandler<UpdateClassCodeCommand, ClassCodeResponse>
{
    private readonly IClassCodeRepository _repository;
    public UpdateClassCodeCommandHandler(IClassCodeRepository repository) => _repository = repository;

    public async Task<ClassCodeResponse> Handle(UpdateClassCodeCommand request, CancellationToken ct)
    {
        var entity = await _repository.GetByIdAsync(request.Id, ct)
            ?? throw new CodeNotFoundException("Class Code", request.Id);   // R2.6 -> 404

        var code = request.Code.Trim();
        if (await _repository.ExistsByCodeAsync(code, request.Id, ct))       // R1.2 excluding self
            throw new DuplicateCodeException("Class Code");                  // -> 409

        entity.Code = code;
        entity.Description = request.Description.Trim();
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(entity, ct);                           // R2.5
        return entity.ToResponse();
    }
}
