using Finnova.Models.Contracts.Assets;
using Finnova.Models.Domain.Entities;
using Finnova.Models.Domain.Exceptions;
using Finnova.Repository.Interfaces;
using Finnova.Service.Mappers;
using MediatR;

namespace Finnova.Service.Assets.MakeCodes.Commands.CreateMakeCode
{
    public class CreateMakeCodeCommandHandler(IMakeCodeRepository makeCodeRepository) : IRequestHandler<CreateMakeCodeCommand, MakeCodeResponse>
    {
        public async Task<MakeCodeResponse> Handle(CreateMakeCodeCommand request, CancellationToken cancellationToken)
        {
            var code = request.Code.Trim();
            if (await makeCodeRepository.ExistsByCodeAsync(code, null, cancellationToken))     // R1.2 / R2.3 duplicate
                throw new DuplicateCodeException("Make Code");          // -> 409 ERR-AST-409

            var entity = new MakeCode
            {
                Code = code,
                Description = request.Description.Trim(),
                IsActive = request.IsActive ?? true,                     // R2.2 default true
            };
            await makeCodeRepository.AddAsync(entity, cancellationToken);                      // R2.1
            return entity.ToResponse();
        }
    }
}

