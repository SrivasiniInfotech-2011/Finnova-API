using FluentValidation;

namespace Finnova.Service.OrganizationHierarchy.Commands.DeleteOrganizationNode;

public class DeleteOrganizationNodeCommandValidator : AbstractValidator<DeleteOrganizationNodeCommand>
{
    public DeleteOrganizationNodeCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id is required.");
    }
}
