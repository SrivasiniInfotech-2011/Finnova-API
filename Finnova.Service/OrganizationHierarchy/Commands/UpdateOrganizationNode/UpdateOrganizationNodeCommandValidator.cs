using FluentValidation;

namespace Finnova.Service.OrganizationHierarchy.Commands.UpdateOrganizationNode;

public class UpdateOrganizationNodeCommandValidator : AbstractValidator<UpdateOrganizationNodeCommand>
{
    public UpdateOrganizationNodeCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id is required.");                  // R3 (target identity)

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")                 // R3.2
            .MaximumLength(150).WithMessage("Name must not exceed 150 characters.");
    }
}

