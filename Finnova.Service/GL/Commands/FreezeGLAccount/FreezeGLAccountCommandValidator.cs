using FluentValidation;

namespace Finnova.Service.GL.Commands.FreezeGLAccount;

public class FreezeGLAccountCommandValidator : AbstractValidator<FreezeGLAccountCommand>
{
    public FreezeGLAccountCommandValidator()
    {
        // R5.2 — mandatory 1..500 char freeze reason
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("A freeze reason is mandatory.")
            .MaximumLength(500).WithMessage("A freeze reason must be 1 to 500 characters.");
    }
}
