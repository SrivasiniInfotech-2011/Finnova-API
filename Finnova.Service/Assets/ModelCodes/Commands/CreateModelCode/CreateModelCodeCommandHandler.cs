using Finnova.Models.Contracts.Assets;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;
using MediatR;

namespace Finnova.Service.Assets.ModelCodes.Commands.CreateModelCode
{
    public class CreateModelCodeCommandHandler(IModelCodeRepository modelCodeRepository) : IRequestHandler<CreateModelCodeCommand, ModelCodeResponse>
    {
        public async Task<ModelCodeResponse> Handle(CreateModelCodeCommand request, CancellationToken cancellationToken)
        {
            var code = request.Code.Trim();
            if (await modelCodeRepository.ExistsByCodeAsync(code, null, cancellationToken))     // R1.2 / R2.3 duplicate
                throw new DuplicateCodeException("Model Code");          // -> 409 ERR-AST-409

            var entity = new ModelCode
            {
                Code = code,
                Description = request.Description.Trim(),
                IsActive = request.IsActive ?? true,                     // R2.2 default true
            };
            await modelCodeRepository.AddAsync(entity, cancellationToken);                      // R2.1
            return entity.ToResponse();
        }
    }
}

