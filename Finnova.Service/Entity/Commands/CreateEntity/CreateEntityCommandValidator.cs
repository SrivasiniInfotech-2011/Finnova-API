using FluentValidation;

namespace Finnova.Service.Entity.Commands.CreateEntity;

public class CreateEntityCommandValidator : AbstractValidator<CreateEntityCommand>
{
    public CreateEntityCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Entity code is required.")
            .MaximumLength(20).WithMessage("Entity code must not exceed 20 characters.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");

        RuleFor(x => x.EntityType).IsInEnum().WithMessage("Entity type is invalid.");

        RuleFor(x => x.RegistrationIdentifier)
            .MaximumLength(50).WithMessage("Registration identifier must not exceed 50 characters.");

        RuleFor(x => x.ContactPerson)
            .MaximumLength(100).WithMessage("Contact person must not exceed 100 characters.");

        RuleFor(x => x.Email)
            .MaximumLength(100).WithMessage("Email must not exceed 100 characters.");

        RuleFor(x => x.Phone)
            .MaximumLength(20).WithMessage("Phone must not exceed 20 characters.");

        RuleFor(x => x.AddressLine)
            .MaximumLength(200).WithMessage("Address line must not exceed 200 characters.");
    }
}