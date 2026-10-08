using FluentValidation;

namespace Finnova.Service.Assets.TypeCodes.Commands.CreateTypeCode
{
    public class CreateTypeCodeCommandValidator : AbstractValidator<CreateTypeCodeCommand>
    {
        public CreateTypeCodeCommandValidator()
        {
            RuleFor(x => x.Code).NotEmpty().WithMessage("Code is required.")
           .MaximumLength(20).WithMessage("Code must not exceed 20 characters.");
            RuleFor(x => x.Description).NotEmpty().WithMessage("Description is required.")
                .MaximumLength(100).WithMessage("Description must not exceed 100 characters.");
        }
    }
}

