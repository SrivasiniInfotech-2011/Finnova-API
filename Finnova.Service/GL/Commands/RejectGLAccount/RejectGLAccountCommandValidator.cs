using FluentValidation;

namespace Finnova.Service.GL.Commands.RejectGLAccount;

public class RejectGLAccountCommandValidator : AbstractValidator<RejectGLAccountCommand>
{
    public RejectGLAccountCommandValidator()
    {
        // R3.6 — mandatory 1..500 char reason
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("A rejection reason is mandatory.")
            .MaximumLength(500).WithMessage("A rejection reason must be 1 to 500 characters.");
    }
}
