using FluentValidation;

namespace Finnova.Service.Court.Commands.CreateCourt;

public class CreateCourtCommandValidator : AbstractValidator<CreateCourtCommand>
{
    public CreateCourtCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Court code is required.")
            .MaximumLength(20).WithMessage("Court code must not exceed 20 characters.");

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