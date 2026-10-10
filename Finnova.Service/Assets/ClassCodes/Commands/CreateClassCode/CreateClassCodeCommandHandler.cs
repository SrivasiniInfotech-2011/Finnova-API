using MediatR;
using Finnova.Models.Contracts.Assets;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;

namespace Finnova.Service.Assets.ClassCodes.Commands.CreateClassCode;

public class CreateClassCodeCommandHandler : IRequestHandler<CreateClassCodeCommand, ClassCodeResponse>
{
    private readonly IClassCodeRepository _repository;
    public CreateClassCodeCommandHandler(IClassCodeRepository repository) => _repository = repository;

    public async Task<ClassCodeResponse> Handle(CreateClassCodeCommand request, CancellationToken ct)
    {
        var code = request.Code.Trim();
        if (await _repository.ExistsByCodeAsync(code, null, ct))     // R1.2 / R2.3 duplicate
            throw new DuplicateCodeException("Class Code");          // -> 409 ERR-AST-409

        var entity = new ClassCode
        {
            Code = code,
            Description = request.Description.Trim(),
            IsActive = request.IsActive ?? true,                     // R2.2 default true
        };
        await _repository.AddAsync(entity, ct);                      // R2.1
        return entity.ToResponse();
    }
}
