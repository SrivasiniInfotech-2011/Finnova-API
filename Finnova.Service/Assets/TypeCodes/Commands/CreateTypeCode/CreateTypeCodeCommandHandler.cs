using Finnova.Models.Contracts.Assets;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;
using MediatR;

namespace Finnova.Service.Assets.TypeCodes.Commands.CreateTypeCode
{
    public class CreateTypeCodeCommandHandler(ITypeCodeRepository typeCodeRepository) : IRequestHandler<CreateTypeCodeCommand, TypeCodeResponse>
    {
        public async Task<TypeCodeResponse> Handle(CreateTypeCodeCommand request, CancellationToken cancellationToken)
        {
            var code = request.Code.Trim();
            if (await typeCodeRepository.ExistsByCodeAsync(code, null, cancellationToken))     // R1.2 / R2.3 duplicate
                throw new DuplicateCodeException("Type Code");          // -> 409 ERR-AST-409

            var entity = new Models.Domain.Entities.TypeCode
            {
                Code = code,
                Description = request.Description.Trim(),
                IsActive = request.IsActive ?? true,                     // R2.2 default true
            };
            await typeCodeRepository.AddAsync(entity, cancellationToken);                      // R2.1
            return entity.ToResponse();
        }
    }
}

