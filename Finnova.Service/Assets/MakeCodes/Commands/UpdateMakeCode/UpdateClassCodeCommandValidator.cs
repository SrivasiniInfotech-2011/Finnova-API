using FluentValidation;

namespace Finnova.Service.Assets.MakeCodes.Commands.UpdateMakeCode;

public class UpdateMakeCodeCommandValidator : AbstractValidator<UpdateMakeCodeCommand>
{
    public UpdateMakeCodeCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().WithMessage("Code is required.")
            .MaximumLength(20).WithMessage("Code must not exceed 20 characters.");
        RuleFor(x => x.Description).NotEmpty().WithMessage("Description is required.")
            .MaximumLength(100).WithMessage("Description must not exceed 100 characters.");
    }
}
