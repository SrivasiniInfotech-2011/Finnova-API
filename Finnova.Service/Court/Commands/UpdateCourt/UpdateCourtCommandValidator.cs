using FluentValidation;

namespace Finnova.Service.Court.Commands.UpdateCourt;

public class UpdateCourtCommandValidator : AbstractValidator<UpdateCourtCommand>
{
    public UpdateCourtCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Id is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(150).WithMessage("Name must not exceed 150 characters.");

        RuleFor(x => x.CourtType).IsInEnum().WithMessage("Court type is invalid.");

        RuleFor(x => x.Jurisdiction)
            .NotEmpty().WithMessage("Jurisdiction is required.")
            .MaximumLength(100).WithMessage("Jurisdiction must not exceed 100 characters.");

        RuleFor(x => x.Location)
            .NotEmpty().WithMessage("Location is required.")
            .MaximumLength(100).WithMessage("Location must not exceed 100 characters.");
    }
}