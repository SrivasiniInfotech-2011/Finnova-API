using FluentValidation;

namespace Finnova.Service.GL.Commands.RejectGLBatch;

public class RejectGLBatchCommandValidator : AbstractValidator<RejectGLBatchCommand>
{
    public RejectGLBatchCommandValidator()
    {
        RuleFor(x => x.Reason)                                           // R9.7
            .NotEmpty().WithMessage("A rejection reason is mandatory.")
            .MaximumLength(500).WithMessage("A rejection reason must be 1 to 500 characters.");
    }
}
