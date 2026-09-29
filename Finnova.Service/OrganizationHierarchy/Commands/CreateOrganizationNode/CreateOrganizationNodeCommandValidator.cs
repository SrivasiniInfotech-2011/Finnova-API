using FluentValidation;

namespace Finnova.Service.OrganizationHierarchy.Commands.CreateOrganizationNode;

/// <summary>
/// Validator class for the CreateOrganizationNodeCommand. It ensures that the command's properties meet the required validation rules, 
/// such as non-empty values and maximum length constraints for the Code and Name fields.
/// </summary>
public class CreateOrganizationNodeCommandValidator : AbstractValidator<CreateOrganizationNodeCommand>
{
    /// <summary>
    /// Initializes a new instance of the CreateOrganizationNodeCommandValidator class and sets up the validation rules for the command's properties.
    /// </summary>
    public CreateOrganizationNodeCommandValidator()
    {
        // R1.3, R2.5 — Code required; R1.4, R2.5 — max 10 characters.
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Organization Node Code is required.")
            .MaximumLength(20).WithMessage("Organization Node Code must not exceed 20 characters.");

        // R1.3 — Name required; R1.4 — max 100 characters.
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(150).WithMessage("Name must not exceed 150 characters.");
    }
}

