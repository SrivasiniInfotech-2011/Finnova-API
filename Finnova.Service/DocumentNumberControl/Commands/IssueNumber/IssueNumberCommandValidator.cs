using Finnova.Models.Domain.Enums;
using FluentValidation;

namespace Finnova.Service.DocumentNumberControl.Commands.IssueNumber;

/// <summary>Validates issuance input (R5.8): document type required; scope id required for Company/Branch.</summary>
public class IssueNumberCommandValidator : AbstractValidator<IssueNumberCommand>
{
    public IssueNumberCommandValidator()
    {
        RuleFor(x => x.DocumentType)
            .NotEmpty().WithMessage("Document type is required.");

        RuleFor(x => x.ScopeId)
            .NotNull()
            .When(x => x.Scope != NumberScope.Global)
            .WithMessage("A scope id is required for company or branch scope.");

        RuleFor(x => x.ScopeId)
            .Null()
            .When(x => x.Scope == NumberScope.Global)
            .WithMessage("A scope id must not be supplied for global scope.");
    }
}
