using FluentValidation;

namespace Finnova.Service.Assets.ClassCodes.Commands.CreateClassCode;

public class CreateClassCodeCommandValidator : AbstractValidator<CreateClassCodeCommand>
{
    public CreateClassCodeCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Code is required.")
            .MaximumLength(20).WithMessage("Code must not exceed 20 characters.");
        RuleFor(x => x.Description).NotEmpty().WithMessage("Description is required.")
            .MaximumLength(100).WithMessage("Description must not exceed 100 characters.");
    }
}
